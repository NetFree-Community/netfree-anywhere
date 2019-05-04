﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Security.Permissions;
using System.ServiceModel;
using System.Windows.Forms;
using System.Threading;
using System.Net.Sockets;
using System.Text.RegularExpressions;


namespace nfaTray
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        string msText = "ms";
        string showText = "הצג";
        string hideText = "הסתר";
        string il = "ישראל";
        string uk = "לונדון";
        string us = "ארצות הברית";
        string ar = "ארגנטינה";
        public enum Languages
        {
            Hebrew = 1,
            English = 2,
            Idish = 3,
            Spanish = 4,
            Russian = 5,
            Espanol = 6
        }
        static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        static string SizeSuffix(Int64 value)
        {
            if (value < 0) { return "-" + SizeSuffix(-value); }
            if (value == 0) { return "0.0 bytes"; }

            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            return string.Format("{0:n1} {1}", adjustedSize, SizeSuffixes[mag]);
        }


        public ObservableCollection<ServerInfo> listServers { get; set; }

        class Client : INfaClientCallback
        {
            MainWindow _app;

            public Client(MainWindow _app)
            {
                this._app = _app;
            }

            public void CallToTray(string state)
            {
                _app.client_onState(state);
            }
        }

        DuplexChannelFactory<INfaServiceNotify> factory = null;

        private string _vpnStatus = null;
        public string vpnStatus
        {
            get
            {
                return _vpnStatus;
            }
            set
            {
                _vpnStatus = value;
                OnPropertyChanged("vpnStatus");
            }
        }

        private string _vpnError = null;
        public string vpnError
        {
            get
            {
                return _vpnError;
            }
            set
            {
                _vpnError = value;
                vpnStatus = (value == null) ? null : "error";
                OnPropertyChanged("vpnError");
                if (value != null)
                {
                    Thread t = new Thread(() =>
                    {
                            for (int i = 0; i < 3 && vpnStatus == "error"; i++)
                            {
                                connectVpn();
                                Thread.Sleep(5000);
                            }
                    });
                }
            }
        }
        public string ipAddress { get; set; }

        INfaServiceNotify service;

        NotifyIcon ni;

        class Tabs
        {
            public const int Main = 0;
            public const int Servers = 1;
            public const int UserPass = 2;
            public const int VpnIdentifier = 3;
            public const int Languages = 4;
        };
        private bool _vpnIdentifierViewed;

        public bool VpnIdentifierViewed
        {
            get
            {
                return _vpnIdentifierViewed;
            }
            set
            {
                _vpnIdentifierViewed = value;
            }
        }
        

        public MainWindow()
        {
            VpnIdentifierViewed = false;

            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            InitializeComponent();

            TabControlWiz.Tag = Visibility.Hidden;
            TabControlWiz.SelectedIndex = Tabs.Main;

            SetWindowPosition();

            if (Properties.Settings.Default.VpnIdentifier.IndexOf(':') == -1)
            {
                TabControlWiz.SelectedIndex = Tabs.VpnIdentifier;
                vpnIdentifierText.Text = Properties.Settings.Default.VpnIdentifier;
                autoConnect.IsChecked = Properties.Settings.Default.AutoConnect;
            }

            ni = new NotifyIcon();
            ni.Text = "NetFree Anywhere";
            ni.Visible = true;
            ni.Icon = nfaTray.Properties.Resources.NetFreeAnywareLogoSq;
            ni.Click += ni_Click;

            SetLanguage((Languages)Properties.Settings.Default.Language);
            ConnectToService();


            this.DataContext = this;


            listServers = new ObservableCollection<ServerInfo>();
            cboServers.ItemsSource = listServers;
            cboServers.SelectionChanged += cboServers_SelectionChanged;

            if (Properties.Settings.Default.AutoConnect && Properties.Settings.Default.VpnIdentifier.IndexOf(':') > -1)
            {
                connectVpn();

            }


        }



        void SetWindowPosition()
        {
            var desktopWorkingArea = SystemParameters.WorkArea;

            var offsetLeft = 30;

            var left = CultureInfo.InstalledUICulture.LCID == 1037 ? 0 + offsetLeft : desktopWorkingArea.Right - Width - offsetLeft;
            Left = left;
            Top = desktopWorkingArea.Bottom - Height;

        }

        void ConnectToService()
        {
            var client = new Client(this);

            var time = TimeSpan.FromSeconds(0.2);
            var binding = new NetNamedPipeBinding
            {
                CloseTimeout = time,
                OpenTimeout = time,
                ReceiveTimeout = time
            };

            if (factory != null)
            {
                try
                {
                    factory.Close();
                }
                catch 
                { 
                }
                
            }

            factory = new DuplexChannelFactory<INfaServiceNotify>(new InstanceContext(client), binding, new EndpointAddress("net.pipe://localhost/netfree-anywhere/control"));
            service = factory.CreateChannel();
            try
            {
                service.SubscribeClient();
            }
            catch (Exception)
            {
                vpnError = "לא ידוע";
            }
            
            
        }


        struct Country
        {
            public string Name;
            public Image Icon;
        }


        private void disInvoke(Action action)
        {
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, action);
        }

        void RefreshServerList()
        {
            var list = nfaServers.GetServers();

            var countryMap = new Dictionary<string, Country>();
            countryMap["il"] = new Country { Name = il };
            countryMap["uk"] = new Country { Name = uk };
            countryMap["us"] = new Country { Name = us };
            countryMap["ar"] = new Country { Name = ar };

            listServers.Clear();
            foreach (var item in list)
            {

                var server = new ServerInfo
                {
                    Server = item,
                    Name = countryMap.ContainsKey(item.Country) ? countryMap[item.Country].Name : item.Country
                };
                listServers.Add(server);



                nfaServers.PingHostTime(item.Host, (t) =>
                {

                    item.Latency = t;
                    server.Speed = t + "ms";
                    disInvoke(() =>
                    {
                        var index = listServers.IndexOf(server);
                        listServers.Remove(server);
                        listServers.Insert(index, server);
                        if (server.Server.Host == Properties.Settings.Default.Host)
                        {
                            cboServers.SelectedValue = server;
                        }
                    });

                });
            }

        }
        
        void client_onState(string state)
        {
            disInvoke(() =>
            {
                client_onState_safe(state);
            });
        }

        void client_onState_safe(string state)
        {

            if (state.StartsWith(">BYTECOUNT:"))
            {
                //>BYTECOUNT:4829,5116
                var bayeCountParams = state.Substring((">BYTECOUNT:").Length).Split(',');
                if (bayeCountParams.Length >= 2)
                {
                    int upload = int.Parse(bayeCountParams[1]);
                    int download = int.Parse(bayeCountParams[0]);

                    UploadRate.Text = SizeSuffix(upload);
                    DownloadRate.Text = SizeSuffix(download);
                }

                if (vpnStatus == "connecting" && DateTime.Now.Subtract(startConnectingTime).TotalSeconds > 20)
                {
                    startConnectingTime = DateTime.Now;
                    vpnStatus = "error";
                    vpnError = "לא מצליח להתחבר";
                    new Thread(() =>
                    {
                        tryService(() =>
                        {
                            service.Disconnect();
                        });
                    }).Start();
                    //if (Properties.Settings.Default.AutoConnect)
                    //{
                    //    for (int i = 0; i < 3 && vpnStatus == "error"; i++)
                    //    {
                    //        Thread.Sleep(3000);
                    //        connectVpn();
                    //    }
                    //}

                }
            }
            if (state.StartsWith(">STATE:"))
            {
                var stateParams = state.Substring((">STATE:").Length).Split(',');
                if (stateParams.Length >= 4)
                {
                    if (stateParams[1] == "CONNECTED" && "SUCCESS" == stateParams[2])
                    {
                        //>STATE:1459521058,CONNECTED,SUCCESS,172.16.1.1,185.18.206.203
                        ipAddress = stateParams[3];
                        VpnIP.Text = ipAddress;
                        VpnHostName.Text = SettingsHost;
                        vpnStatus = "connected";
 
                    }

                    if (stateParams[1] == "EXITING")
                    {
                        //>STATE:1459625014,EXITING,auth-failure,,
                        vpnStatus = null;
                        if ("auth-failure" == stateParams[2])
                        {
                            vpnError = "שם משתמש וסיסמה שגויים";
                        }
                        else
                        {
                            vpnError = "לא ידוע";
                        }
                    }
                }
            }
            //>FATAL:There are no TAP-Windows adapters on this system.  You should be able tocreate a TAP-Windows adapter by going to Start -> All Programs -> TAP-Windows ->Utilities -> Add a new TAP-Windows virtual ethernet adapter.
            if (state.StartsWith(">FATAL:"))
            {
                var errorString = state.Substring((">FATAL:").Length);

                if (errorString.Contains("no TAP-Windows"))
                {
                    vpnError = "רכיב TAP לא מותקן";
                }
                else
                {
                    vpnError = errorString;
                }
            }


            Console.WriteLine(state);
        }


        void win_onConnect(string obj)
        {

        }

        void ni_Click(object sender, EventArgs e)
        {
            this.SetWindowPosition();
            this.Show();
            this.Activate();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }



        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }



        string SettingsHost = "";
        DateTime startConnectingTime = DateTime.Now;

        private void connectVpn()
        {

            if (vpnStatus == "connecting") return;

            vpnStatus = "connecting";


            ProtocolType proto = ProtocolType.Unknown;
            string hostName = Properties.Settings.Default.Host;
            string userName = "";
            string password = "";
            int port = 0;

            Properties.Settings.Default.VpnIdentifier.Replace(" ", "");
            for (int i = 0; i < Properties.Settings.Default.VpnIdentifier.Length; i++)
            {
                if (char.IsControl(Properties.Settings.Default.VpnIdentifier.ToCharArray()[i]))
                {
                    Properties.Settings.Default.VpnIdentifier.Replace(Properties.Settings.Default.VpnIdentifier.ToCharArray()[i].ToString(), "");
                }
            }


            Regex VpnIdentifierRegex = new Regex("^((?<proto>tcp|udp)://)?(?<user>[^:]+):(?<pass>[^@]+)(@(?<host>[^:]+)(:(?<port>\\d+))?)?");

            Match match = VpnIdentifierRegex.Match(Properties.Settings.Default.VpnIdentifier);

            if (match.Success)
            {
                proto = match.Groups["proto"].Value == "tcp" ? ProtocolType.Tcp : (match.Groups["proto"].Value == "udp" ? ProtocolType.Udp : ProtocolType.Unknown) ;
                hostName = match.Groups["host"].Value != "" ? match.Groups["host"].Value : hostName;
                userName = match.Groups["user"].Value;
                password = match.Groups["pass"].Value;
                port = match.Groups["port"].Value != "" ? int.Parse(match.Groups["port"].Value) : port;
            }
            if ((bool)udpProtocol.IsChecked)
                proto = ProtocolType.Udp;

 
            Action hasPort = () =>
            {

                if (port <= 0)
                {
                    disInvoke(() =>
                    {
                        vpnStatus = "error";
                        vpnError = "לא מוצא פורט פעיל";
                    });

                    return;
                }

                disInvoke(() =>
                {
                    SettingsHost = hostName;
                });

                startConnectingTime = DateTime.Now;

                tryService(() =>
                {
                    service.Connect(hostName, port, userName, password, proto);
                });
            };



            int[] portsListUdp = { 26, 53, 80 , 123, 137, 443, 1023, 1812, 2083, 5060 };
            int[] portsListTcp = { 21, 25, 53, 80, 110, 143, 443, 1000, 1433, 3306, 5060 };

            Action hasHost = () =>
            {

                if (port > 0 && (proto == ProtocolType.Tcp || proto == ProtocolType.Udp))
                {
                    hasPort();
                    return;
                }


                if (proto == ProtocolType.Unknown || proto == ProtocolType.Tcp)
                {
                    nfaServers.findOpenPortTcp(hostName, portsListTcp.OrderBy(a => Guid.NewGuid()).ToArray(), (portTcp ) =>
                    {

                        if (portTcp > 0)
                        {
                            proto = ProtocolType.Tcp;
                            port = portTcp;
                            hasPort();
                            return;
                        }

                        nfaServers.findOpenPortUdp(hostName, portsListUdp.OrderBy(a => Guid.NewGuid()).ToArray(), (portUdp) =>
                        {
                            if (portUdp > 0)
                            {
                                proto = ProtocolType.Udp;
                                port = portUdp;
                            }

                            hasPort(); 
                        });
                    });
                }
                else
                {

                    nfaServers.findOpenPortUdp(hostName, portsListUdp.OrderBy(a => Guid.NewGuid()).ToArray(), (portUdp) =>
                    {
                        if (portUdp > 0)
                        {
                            proto = ProtocolType.Udp;
                            port = portUdp;
                            hasPort();
                            return;
                        }
                        nfaServers.findOpenPortTcp(hostName, portsListTcp.OrderBy(a => Guid.NewGuid()).ToArray(), (portTcp) =>
                        {

                            if (portTcp > 0)
                            {
                                proto = ProtocolType.Tcp;
                                port = portTcp;
                            }

                            hasPort(); 
                        });
                    });
                }
            };


            if (hostName.Length > 2)
            {
                hasHost();
            }
            else
            {

                var list = nfaServers.GetServers();

                if (list.Count == 0)
                {
                    vpnStatus = "error";
                    vpnError = "לא נמצא שרת נטפרי";
                    return;
                }

                var listHost = new List<string>();
                foreach (var item in list)
                {
                    listHost.Add(item.Host);
                }

                nfaServers.findFastHost(listHost.ToArray(), (host, t) =>
                {
                    hostName = host;
                    hasHost();
                });
            }
            for (int i = 0; i < 3 && vpnStatus == "error"; i++)
            {
                ConnectToService();
            }

        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            connectVpn();
        }

        private void btnDisconnect_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            new Thread(() =>
            {
                tryService(() =>
                {
                    service.Disconnect();
                });
            }).Start();

            vpnStatus = null;
        }

        private void btnSelectServer_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Window_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void Return_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            TabControlWiz.SelectedIndex = Tabs.Main;
        }

        private void selectServer_Click(object sender, RoutedEventArgs e)
        {
            RefreshServerList();
            TabControlWiz.SelectedIndex = Tabs.Servers;
        }

        private void btnSaveUserPass_Click(object sender, RoutedEventArgs e)
        {
            //Properties.Settings.Default.User = iptUser.Text;
            //Properties.Settings.Default.Password = iptPassword.Text;
            //Properties.Settings.Default.Save();

            //TabControlWiz.SelectedIndex = Tabs.Main;
        }

        private void changeVpnIdentifier_Click(object sender, RoutedEventArgs e)
        {
            //iptUser.Text = Properties.Settings.Default.User;
            //iptPassword.Text = Properties.Settings.Default.Password;

            vpnIdentifierPass.Password = Properties.Settings.Default.VpnIdentifier;
            autoConnect.IsChecked = Properties.Settings.Default.AutoConnect;
            TabControlWiz.SelectedIndex = Tabs.VpnIdentifier;
        }

        private void TabControlWiz_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        void cboServers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var org = e.OriginalSource;
            var va = e.Source;
            var seleted = (ServerInfo)cboServers.SelectedValue;
            if (seleted != null)
            {
                Properties.Settings.Default.Host = seleted.Server.Host;
            }
            else
            {
                Properties.Settings.Default.Host = "";
            }

            Properties.Settings.Default.Save();
        }


        private void autoSelect_Click(object sender, RoutedEventArgs e)
        {

            cboServers.SelectedIndex = -1;
        }

        private void btnSaveVpnIdentifier_Click(object sender, RoutedEventArgs e)
        {
            if (!VpnIdentifierViewed)
            {
                vpnIdentifierText.Text = vpnIdentifierPass.Password;
            }
            Properties.Settings.Default.VpnIdentifier = vpnIdentifierText.Text;
            Properties.Settings.Default.AutoConnect = autoConnect.IsChecked ?? false;
            Properties.Settings.Default.udpProtocol = udpProtocol.IsChecked ?? false;
            Properties.Settings.Default.Save();
            TabControlWiz.SelectedIndex = Tabs.Main;
        }


        private void tryService(Action  work)
        {
            try
            {
                work();
            }
            catch (CommunicationException e)
            {
                ConnectToService();
                if (e is System.ServiceModel.CommunicationObjectFaultedException)
                {
                    tryService(work);
                }
            }
            catch (TimeoutException e)
            {
                ConnectToService();
            }
            catch (Exception e)
            {
                ConnectToService();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!VpnIdentifierViewed)
            {
                vpnIdentifierText.Text = vpnIdentifierPass.Password;
                vpnIdentifierPass.Visibility = Visibility.Collapsed;
                vpnIdentifierText.Visibility = Visibility.Visible;
                button.Content = hideText;
                VpnIdentifierViewed = true;
            }
            else
            {
                vpnIdentifierPass.Password = vpnIdentifierText.Text;
                vpnIdentifierPass.Visibility = Visibility.Visible;
                vpnIdentifierText.Visibility = Visibility.Collapsed;
                button.Content = showText;
                VpnIdentifierViewed = false;
            }
        }
        public void SetLanguage(Languages lang)
        {
            switch (lang)
            {
                case Languages.Hebrew:
                    mainGrid.FlowDirection = System.Windows.FlowDirection.RightToLeft;
                    // Main
                    ni.Text = "NetFree Anywhere";
                    btnConnect.Content = "התחבר";
                    btnDisconnect.Content = "התנתק";
                    // Change Connect ID
                    changeUserPass.Content = "שנה מזהה חיבור";
                    ConnectIDLabel.Content = "מזהה חיבור";
                    AutoConnectLabel.Text = "התחברות אוטומטית";
                    udpProtocolLabel.Text = "התחברות באמצעות udp";
                    showText = "הצג";
                    hideText = "הסתר";
                    button.Content = "הצג";
                    btnSaveVpnIdentifier.Content = "שמור";
                    // Select server
                    selectServer.Content = "בחר שרת";
                    autoSelect.Content = "בחר שרת אוטומטית";
                    il = "ישראל";
                    uk = "לונדון";
                    us = "ארצות הברית";
                    ar = "ארגנטינה";
                    // Language
                    selectLanguage.Content = "בחר שפה";
                    button1.Content = "שמור שפה";
                    break;
                case Languages.English:
                    mainGrid.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                    // Main
                    ni.Text = "NetFree Anywhere";
                    btnConnect.Content = "Connect";
                    btnDisconnect.Content = "Disconnect";
                    // Change Connect ID
                    changeUserPass.Content = "Change Connection ID";
                    ConnectIDLabel.Content = "Connection ID";
                    AutoConnectLabel.Text = "Connect Automatically";
                    udpProtocolLabel.Text = "Connect via UDP";
                    showText = "Show";
                    hideText = "Hide";
                    button.Content = "Show";
                    btnSaveVpnIdentifier.Content = "Save";
                    // Select server
                    selectServer.Content = "Select a Server";
                    autoSelect.Content = "Selct server Automatically";
                    il = "Israel";
                    uk = "London";
                    us = "United States";
                    ar = "Argentina";
                    // Language
                    selectLanguage.Content = "Change language";
                    button1.Content = "Save language";
                    break;
                case Languages.Idish:
                    mainGrid.FlowDirection = System.Windows.FlowDirection.RightToLeft;
                    // Main
                    ni.Text = "NetFree Anywhere";
                    btnConnect.Content = "פארבינד זיך";
                    btnDisconnect.Content = "האק אפ";
                    // Change Connect ID
                    changeUserPass.Content = "טויש די קאַנעקשאַן דעטאלן";
                    ConnectIDLabel.Content = "קאַנעקשאַן דעטאלן";
                    AutoConnectLabel.Text = "זיך צו פארבינדן אָטאַמאַטיש";
                    udpProtocolLabel.Text = "פארבינדן דורך udp";
                    showText = "בּאַווייז";
                    hideText = "בּאַהאַלט";
                    button.Content = "בּאַווייז";
                    btnSaveVpnIdentifier.Content = "געדענק די אינפאָרמאַציע";
                    // Select server
                    selectServer.Content = "וועל אויס א סערווער";
                    autoSelect.Content = "וועל א סערווער אָטאָמאַטיש";
                    il = "ארץ ישראל";
                    uk = "לאָנדאָן";
                    us = "אמעריקע";
                    ar = "ארגענטינע";
                    // Language
                    selectLanguage.Content = "טויש א שפּראַך";
                    button1.Content = "געדענק די שפּראַך";
                    break;
                case Languages.Spanish:
                    mainGrid.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                    // Main
                    ni.Text = "NetFree en cualquier lugar";
                    btnConnect.Content = "Conectar";
                    btnDisconnect.Content = "Desconectar";
                    // Change Connect ID
                    changeUserPass.Content = "Cambiar ID de conexión";
                    ConnectIDLabel.Content = "ID de conexión";
                    AutoConnectLabel.Text = "Conexión automática";
                    udpProtocolLabel.Text = " Conectar usando udp";
                    showText = "Mostrar";
                    hideText = "Ocultar";
                    button.Content = "Mostrar";
                    btnSaveVpnIdentifier.Content = "Guardar";
                    // Select server
                    selectServer.Content = "Seleccionar servidor";
                    autoSelect.Content = "Seleccionar servidor automáticamente";
                    il = "Israel";
                    uk = "Londres";
                    us = "Estados Unidos";
                    ar = "Argentina";
                    msText = "ms";
                    // Language
                    selectLanguage.Content = "Cambiar idioma";
                    button1.Content = "Guardar idioma";
                    break;
                case Languages.Russian:
                    mainGrid.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                    // Main
                    ni.Text = "NetFree Anywhere";
                    btnConnect.Content = "Подключиться";
                    btnDisconnect.Content = "Отключиться";
                    // Change Connect ID
                    changeUserPass.Content = "Изменить идентификатор подключения";
                    ConnectIDLabel.Content = "Идентификатор подключения";
                    AutoConnectLabel.Text = "Автоматически подключаться";
                    udpProtocolLabel.Text = "Подключиться используя UDP";
                    showText = "Вывести на экран";
                    hideText = "Убрать с экрана";
                    button.Content = "Вывести на экран";
                    btnSaveVpnIdentifier.Content = "Сохранение";
                    // Select server
                    selectServer.Content = "Выбор сервера";
                    autoSelect.Content = "Выбрать сервер автоматически";
                    il = "Израиль";
                    uk = "Лондон";
                    us = "США";
                    ar = "Аргентина";
                    msText = "мс";
                    // Language
                    selectLanguage.Content = "изменение языка";
                    button1.Content = "Сохранить язык";
                    break;
                case Languages.Espanol:
                    mainGrid.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                    // Main
                    ni.Text = "NetFree Anywhere";
                    btnConnect.Content = "connecter";
                    btnDisconnect.Content = "Déconnecter";
                    // Change Connect ID
                    changeUserPass.Content = "changer l'identifiant de connexion";
                    ConnectIDLabel.Content = "identifiant de connexion";
                    AutoConnectLabel.Text = "Connexion automatique";
                    udpProtocolLabel.Text = "connexion via udp";
                    showText = "Afficher";
                    hideText = "Cacher";
                    button.Content = "Afficher";
                    btnSaveVpnIdentifier.Content = "sauvegarder";
                    // Select server
                    selectServer.Content = "Sélectionnez le serveur";
                    autoSelect.Content = "Sélectionner un serveur automatiquement";
                    il = "Israël";
                    uk = "Argentine";
                    us = "États Unis";
                    ar = "Londres";
                    msText = "ms";
                    // Language
                    selectLanguage.Content = "Changer de langue";
                    button1.Content = "Enregistrer la langue";
                    break;
                default:
                    break;
            }
            

        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            if (heItem.IsSelected)
            {
                Properties.Settings.Default.Language = 1;
            }
            else if (enItem.IsSelected)
            {
                Properties.Settings.Default.Language = 2;
            }
            else if (idItem.IsSelected)
            {
                Properties.Settings.Default.Language = 3;
            }
            else if (spItem.IsSelected)
            {
                Properties.Settings.Default.Language = 4;
            }
            else if (ruItem.IsSelected)
            {
                Properties.Settings.Default.Language = 5;
            }
            else if (esItem.IsSelected)
            {
                Properties.Settings.Default.Language = 6;
            }
            SetLanguage((Languages)Properties.Settings.Default.Language);
            TabControlWiz.SelectedIndex = Tabs.Main;
        }

        private void SelectLanguage_Click(object sender, RoutedEventArgs e)
        {
            TabControlWiz.SelectedIndex = Tabs.Languages;
        }
    }

    public class ServerInfo
    {
        public string Name { get; set; }
        public string Speed { get; set; }
        public nfaServer Server { get; set; }
    }

 
}
