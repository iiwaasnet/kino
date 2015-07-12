using System;
using NetMQ;

namespace rawf.Connectivity
{
    public interface IConnectivityProvider : IDisposable
    {
        //TODO: Might extract full NetMQContext interface later
        NetMQContext GetConnectivityContext();
    }
}