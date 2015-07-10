using System;
using NetMQ;

namespace rawf.Connectivity
{
    public class ConnectivityProvider : IConnectivityProvider
    {
        private readonly NetMQContext context;

        public ConnectivityProvider()
        {
            context = NetMQContext.Create();
        }

        public IDisposable GetConnectivityContext()
        {
            return context;
        }

        public void Dispose()
        {
            context.Dispose();
        }
    }
}