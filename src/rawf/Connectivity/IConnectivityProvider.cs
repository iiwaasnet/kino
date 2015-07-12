using System;
using NetMQ;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public interface IConnectivityProvider : IDisposable
    {
        //TODO: Might extract full NetMQContext interface later
        NetMQContext GetConnectivityContext();

        ISocket CreateDealerSocket();
    }
}