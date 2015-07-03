using System;
using NetMQ;

namespace rawf.Connectivity
{
    public class ConnectivityProvider : IConnectivityProvider
    {
        private readonly NetMQContext context;
        private readonly string localEndpointAddress;

        public ConnectivityProvider(string localEndpointAddress)
        {
            this.localEndpointAddress = localEndpointAddress;
            context = NetMQContext.Create();
        }

        public IDisposable GetConnectivityContext()
        {
            return context;
        }

        public string GetLocalEndpointAddress()
        {
            return localEndpointAddress;
        }

        public void Dispose()
        {
            context.Dispose();
        }
    }
}