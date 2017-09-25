using System;
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
        };

        public MainWindow()
        {

            InitializeComponent();

            TabControlWiz.Tag = Visibility.Hidden;
            TabControlWiz.SelectedIndex = Tabs.Main;

            SetWindowPosition();

            if (Properties.Settings.Default.VpnIdentifier.IndexOf(':') == -1)
            {
                TabControlWiz.SelectedIndex = Tabs.VpnIdentifier;
                vpnIdentifier.Text = Properties.Settings.Default.VpnIdentifier;
                autoConnect.IsChecked = Properties.Settings.Default.AutoConnect;
            }

            ni = new NotifyIcon();
            ni.Text = "NetFree Anywhere";
            ni.Visible = true;
            ni.Icon = nfaTray.Properties.Resources.NetFreeAnywareLogoSq;
            ni.Click += ni_Click;


            ConnectToService();


            this.DataContext = this;


            listServers = new ObservableCollection<ServerInfo>();
            cboServers.ItemsSource = listServers;
            cboServers.SelectionChanged += cboServers_SelectionChanged;

            if (Properties.Settings.Default.AutoConnect && Properties.Settings.Default.VpnIdentifier.IndexOf(':')>-1)
                connectVpn();

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
            countryMap["il"] = new Country { Name = "ישראל" };
            countryMap["uk"] = new Country { Name = "לונדון" };
            countryMap["us"] = new Country { Name = "ארצות הברית" };

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



            int[] portsListUdp = { 26, 53, 123, 137, 1023, 1812, 2083, 5060 };
            int[] portsListTcp = { 21, 25, 53, 80, 110, 143, 443, 1000, 1433, 3306, 5060 };

            Action hasHost = () =>
            {

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

            vpnIdentifier.Text = Properties.Settings.Default.VpnIdentifier;
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
            Properties.Settings.Default.VpnIdentifier = vpnIdentifier.Text;
            Properties.Settings.Default.AutoConnect = autoConnect.IsChecked??false;
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

    }

    public class ServerInfo
    {
        public string Name { get; set; }
        public string Speed { get; set; }
        public nfaServer Server { get; set; }
    }

 
}
