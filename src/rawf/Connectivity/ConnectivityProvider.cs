using System;
using NetMQ;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public class ConnectivityProvider : IConnectivityProvider
    {
        private readonly NetMQContext context;

        public ConnectivityProvider()
        {
            context = NetMQContext.Create();
        }

        public NetMQContext GetConnectivityContext()
        {
            return context;
        }

        public ISocket CreateDealerSocket()
        {
            return new Socket(context.CreateDealerSocket());
        }

        public void Dispose()
        {
            context.Dispose();
        }
    }
}