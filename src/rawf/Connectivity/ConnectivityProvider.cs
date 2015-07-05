using System;
using System.Collections.Generic;
using NetMQ;

namespace rawf.Connectivity
{
    public class ConnectivityProvider : IConnectivityProvider
    {
        private readonly NetMQContext context;
        private readonly string localEndpointAddress;
        private readonly string localPeerAddress;
        private readonly IEnumerable<string> peerAddresses;

        public ConnectivityProvider(string localEndpointAddress, string localPeerAddress, string peerAddress)
        {
            this.localEndpointAddress = localEndpointAddress;
            this.localPeerAddress = localPeerAddress;
            peerAddresses = new[] {peerAddress};
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

        public string GetLocalPeerAddress()
        {
            return localPeerAddress;
        }

        public IEnumerable<string> GetPeerAddresses()
        {
            return peerAddresses;
        }

        public void Dispose()
        {
            context.Dispose();
        }
    }
}