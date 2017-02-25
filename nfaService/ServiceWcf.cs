using System;
using System.Collections.Generic;
using System.ServiceModel;

[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Reentrant, IncludeExceptionDetailInFaults = true, AutomaticSessionShutdown = false)]
internal class ServiceImplementation : INfaServiceNotify
{

    public static nfaService.IPCServiceConn conn;

    public void SubscribeClient()
    {
        var current = OperationContext.Current.GetCallbackChannel<INfaClientCallback>();
        System.Threading.Tasks.Task.Factory.StartNew(() =>
        {
            if (conn != null) conn.AddTray(current);
        });
    }

    public void UnSubscribeClient()
    {
        var chanel = OperationContext.Current.GetCallbackChannel<INfaClientCallback>();
        if (conn != null) conn.RemoveTray(chanel);
    }

    public void Connect(string server, int port, string user, string pass , System.Net.Sockets.ProtocolType proto)
    {
        if (conn != null) conn.Connect(server, port, user, pass, proto);
    }

    public void Disconnect()
    {
        if (conn != null) conn.Disconnect();
    } 

    public long Ping()
    {
        return  DateTime.Now.Ticks;
    }
    
}



