using System.Collections.Generic;
using System.ServiceModel;

public interface INfaClientCallback
{
    [OperationContract(IsOneWay = true)]
    void CallToTray(string state);
}

[ServiceContract(CallbackContract = typeof(INfaClientCallback))]
public interface INfaServiceNotify
{
    [OperationContract]
    void SubscribeClient();

    [OperationContract]
    void UnSubscribeClient();

    [OperationContract]
    void Connect(string server, int port, string user, string pass, System.Net.Sockets.ProtocolType protocol );

    [OperationContract]
    void Disconnect();

    [OperationContract]
    long Ping();
}


