using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceProcess;

namespace nfaService
{
    class Program
    {



        static void Main(string[] args)
        {
            if (!Environment.UserInteractive)
            {
                var ind = new ServiceS();
                ServiceBase.Run(ind);
            }
            else
            {
                oVpnConnetion vpn = new oVpnConnetion();
                Console.Read();
                vpn.Disconnect();
            }
        }

    }

    public class ServiceS : ServiceBase
    {
        oVpnConnetion vpn;

        protected override void OnStart(string[] args)
        {
            Trace.WriteLine("start service OnStartPoint");
            base.OnStart(args);

            try
            {
                Trace.WriteLine("start the interface check");
                vpn = new oVpnConnetion();
            }
            catch (Exception ex)
            {
                Trace.TraceError("filed to create interface check instance. the error:\n" + ex.ToString());
                Stop();
            }
        }

        protected override void OnStop()
        {
            vpn.Disconnect();
            base.OnStop();
        }
    }
}