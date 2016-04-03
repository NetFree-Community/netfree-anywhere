using System.Collections.Generic;
using System.ServiceModel;

public interface INfgClientCallback
{
    [OperationContract(IsOneWay = true)]
    void CallToTray(string state);
}

[ServiceContract(CallbackContract = typeof(INfgClientCallback))]
public interface INfgServiceNotify
{
    [OperationContract]
    void SubscribeClient();

    [OperationContract]
    void UnSubscribeClient();

    [OperationContract]
    void Connect(string server, int port, string user, string pass);

    [OperationContract]
    void Disconnect();
}


