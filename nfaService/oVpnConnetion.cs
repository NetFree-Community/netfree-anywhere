using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.ServiceModel;

namespace nfaService
{
    class IPCServiceConn
    {
        private oVpnConnetion oVpnConnetion;
        private readonly List<INfaClientCallback> TarysChanel = new List<INfaClientCallback>();
        ServiceHost host;
        public IPCServiceConn(oVpnConnetion oVpnConnetion)
        {
            this.oVpnConnetion = oVpnConnetion;
            ServiceImplementation.conn = this;

            oVpnConnetion.onState += oVpnConnetion_onState;
            host = new ServiceHost(typeof(ServiceImplementation), new Uri(@"net.pipe://localhost/netfree-anywhere/"));
            host.AddServiceEndpoint(typeof(INfaServiceNotify), new NetNamedPipeBinding(), "control");
            host.Open();

        }

        public void AddTray(INfaClientCallback chanel)
        {
            TarysChanel.Add(chanel);
        }

        public void RemoveTray(INfaClientCallback chanel)
        {
            TarysChanel.Remove(chanel);
        }

        public void Connect(string ip, int port, string user, string pass, System.Net.Sockets.ProtocolType proto){
            Console.WriteLine("get connect to: " + ip + ":" + port.ToString() );
            this.oVpnConnetion.ConnectToVPN(ip, port, user, pass, proto);
        }

        public void Disconnect()
        {
            this.oVpnConnetion.Disconnect();
        }

        void oVpnConnetion_onState(string state)
        {

            for (int i = TarysChanel.Count - 1; i > -1; i--)
                try
                {
                    TarysChanel[i].CallToTray(state);
                }
                catch (Exception)
                {
                    TarysChanel.RemoveAt(i);
                }
        }
        public void Close()
        {
            try
            {
                host.Close();
            }
            finally
            {
            }
        }
    }

    class oVpnConnetion
    {
        static string dirName = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

        public Thread connectTread = null;
        public bool goDiconnect = false; 

        public oVpnConnetion()
        {
            new IPCServiceConn(this);
        }

        public void Disconnect()
        {

            if (connectTread != null && connectTread.IsAlive)
            {
                goDiconnect = true;
                connectTread.Join();
            }
            connectTread = null;
        }

        public void ConnectToVPN(string ip, int port, string user, string pass,System.Net.Sockets.ProtocolType proto)
        {

            if(connectTread != null && connectTread.IsAlive){
                goDiconnect = true;
                connectTread.Join();
            }

            connectTread = new Thread(() =>
            {
                ConnectToVPNWorker(ip, port, user, pass,proto);

            });

            connectTread.Start();
        }

        private void ConnectToVPNWorker(string ip, int port, string user, string pass, System.Net.Sockets.ProtocolType proto)
        {


            string appDir = Path.GetDirectoryName( System.Reflection.Assembly.GetExecutingAssembly().Location );
            string pathConfig = Path.Combine(appDir,".nfa-config.ovpn");
            string pathPass = Path.Combine(appDir, ".nfa-pass.txt");
            File.WriteAllText(pathPass, user + "\n" + pass);

            string ovpnConfig = Properties.Resources.ovpn_template;


            ovpnConfig = ovpnConfig.Replace("{{remote_ip_port}}", ip + " " + port.ToString() );
            ovpnConfig = ovpnConfig.Replace("{{user_pass}}", "\n" + user + "\n" + pass + "\n" );
            ovpnConfig = ovpnConfig.Replace("{{user_pass_path}}", pathPass.Replace(@"\",@"\\"));
            ovpnConfig = ovpnConfig.Replace("{{proto}}", proto == ProtocolType.Udp ? "udp" : "tcp");
            
            File.WriteAllText(pathConfig, ovpnConfig);


            var pInfo  = new ProcessStartInfo();
            
            //pInfo.WindowStyle = ProcessWindowStyle.Hidden;
            pInfo.FileName =  Path.Combine(dirName , @"openvpn\openvpn.exe");
            pInfo.Arguments = "\"" + pathConfig + "\"";

            
            pInfo.RedirectStandardError = true;
            pInfo.RedirectStandardOutput = true;
            pInfo.RedirectStandardInput = true;

            //pInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            //pInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

            pInfo.UseShellExecute = false;

            var p = Process.Start(pInfo);
            
            p.OutputDataReceived += p_OutputDataReceived;
            p.ErrorDataReceived += p_OutputDataReceived;

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            
            Thread.Sleep(100);

            TcpClient client = null;
            var tryCount = 5;

            while (client == null && tryCount-- > 0)
            {
                try
                {
                    client = new TcpClient("127.0.0.1", 7000);
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }

            }

            if (client == null)
            {
                return;
            }
            
            var stream = client.GetStream();

            var telnet = new Telnet(stream);
            
            telnet.WriteLine("state on");
            //telnet.WriteLine("log all on");
            //telnet.WriteLine("hold off");
            //telnet.WriteLine("hold release");
            telnet.WriteLine("bytecount 1");

            int countToDiconnect = 0;
            while (true)
            {
                string line = telnet.GetLine();
               
               
                if(line != null){
                    if (onState != null && line.StartsWith(">"))
                    {
                        onState.Invoke(line);
                    }
                    Console.WriteLine(line);
                }

                if (goDiconnect)
                {
                    if (p.HasExited)
                    {
                        break;
                    }
                    else if (countToDiconnect > 10) //2s
                    {
                        break;
                    }
                    else if (countToDiconnect == 0)
                    {
                        telnet.WriteLine("signal SIGHUP");
                    }

                    countToDiconnect++;
                }

                if (line == null) 
                    Thread.Sleep(200);
            }

            p.WaitForExit(200);

            goDiconnect = false;

            if (!p.HasExited)
            {
                p.Kill();
            }
        }

        private void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.Write("fw: " + e.Data);
        }

        public event Action<string> onState;

    }

	public class Telnet
	{
		#region "Fields"

		private StringBuilder buffer = new StringBuilder();
		private LinkedList<string> linee = new LinkedList<string>();

		private NetworkStream m_NetStream;
		#endregion

		#region "Constructors"

		public Telnet(NetworkStream netStream)
		{
			m_NetStream = netStream;
			m_NetStream.ReadTimeout = 1000;
		}

		#endregion

		#region "Methods"

		public string GetLine()
		{
			while (m_NetStream.DataAvailable) {
				int singlebyte = m_NetStream.ReadByte();
				if (singlebyte >= 0) {
					if (singlebyte == 10 || singlebyte == 13) {
						if (buffer.Length > 0) {
							linee.AddLast(buffer.ToString());
							buffer = new StringBuilder();
						}
					} else {
						buffer.Append(Convert.ToChar(Convert.ToByte(singlebyte)));
					}
				}
			}

			if (linee.Count > 0) {
				string str = linee.First.Value;
				linee.RemoveFirst();

				return str;
			}

			return null;
		}

		public void WriteLine(string line)
		{
			try {
				line = line + Environment.NewLine;
				byte[] array = System.Text.Encoding.Default.GetBytes(line);
				m_NetStream.Write(array, 0, array.Length);


			} catch (Exception ex) {
			}
		}

		#endregion
	}
}
