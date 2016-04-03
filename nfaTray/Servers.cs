
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Heijden.DNS;


namespace nfaTray
{

    public class nfgServers
    {

        public static List<nfgServer> GetServers()
        {

            var list = new List<string>();
            
            Resolver resolver = new Resolver();
            Response response = resolver.Query("servers.ovpn.nfg.netfree.link", QType.TXT);
            foreach (AnswerRR answerRR in response.Answers){
                var str  = answerRR.RECORD.ToString();
                list.Add(str.Substring(1,str.Length-2));
            }
                

            var listServers = new List<nfgServer>();

            foreach (string item in list)
            {
                var tmp = item.Split('|');

                if (tmp.Length > 1)
                {
                    var server = new nfgServer
                    {
                        Country = tmp[1],
                        Host = tmp[0],
                        Latency = -1
                    };
                    listServers.Add(server);
                }
            }

            return listServers;

        }

        public static void PingHostTime(string nameOrAddress, Action<long> callback)
        {
            new System.Threading.Thread(() =>
            {
                Ping pinger = new Ping();
                try
                {
                    PingReply reply = pinger.Send(nameOrAddress, 1000);

                    if (reply.Status == IPStatus.Success)
                    {
                        callback.Invoke(reply.RoundtripTime);
                        return;
                    }
                }
                catch (PingException generatedExceptionName)
                {

                }
                callback.Invoke(-1);
            }).Start();
        }
    }
    public class nfgServer
    {
        public long Latency { get; set; }
        public string Country { get; set; }
        public string Host { get; set; }
    }


}
