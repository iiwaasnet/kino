using System;
using System.Collections.Generic;
using System.Linq;
using rawf.Framework;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public class ConnectivityProvider : IConnectivityProvider
    {
        private readonly ISocketFactory socketFactory;
        private readonly INodeConfiguration nodeConfiguration;
        private readonly IClusterConfiguration clusterConfiguration;
        private static readonly byte[] RouterIdentity = {0, 1, 2, 3};
        private static readonly byte[] ScaleOutSocketIdentity = {4, 5, 6, 7};

        public ConnectivityProvider(ISocketFactory socketFactory, INodeConfiguration nodeConfiguration, IClusterConfiguration clusterConfiguration)
        {
            this.socketFactory = socketFactory;
            this.nodeConfiguration = nodeConfiguration;
            this.clusterConfiguration = clusterConfiguration;
        }

        public ISocket CreateRouterSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetMandatoryRouting();
            socket.SetIdentity(RouterIdentity);
            socket.Bind(nodeConfiguration.RouterAddress);

            return socket;
        }

        public ISocket CreateScaleOutFrontendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetIdentity(ScaleOutSocketIdentity);
            socket.SetMandatoryRouting();
            socket.Connect(nodeConfiguration.RouterAddress);
            socket.Bind(nodeConfiguration.ScaleOutAddress);

            return socket;
        }

        public ISocket CreateScaleOutBackendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetIdentity(ScaleOutSocketIdentity);
            socket.SetMandatoryRouting();
            foreach (var peer in clusterConfiguration.GetClusterMembers())
            {
                socket.Connect(peer.Address);
            }

            return socket;
        }

        public ISocket CreateActorSyncSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            socket.Connect(nodeConfiguration.RouterAddress);

            return socket;
        }

        public ISocket CreateActorAsyncSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(nodeConfiguration.RouterAddress);

            return socket;
        }

        public ISocket CreateMessageHubSendingSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(nodeConfiguration.RouterAddress);

            return socket;
        }

        public ISocket CreateMessageHubReceivingSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            socket.Connect(nodeConfiguration.RouterAddress);

            return socket;
        }

        public ISocket CreateClusterEventsSocket()
        {
            throw new NotImplementedException();
        }

        public ISocket CreateHeartBeatSocket()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<NodeIdentity> GetClusterIdentities()
        {
            return clusterConfiguration.GetClusterMembers().Select(m => new NodeIdentity {Value = m.Identity});
        }
    }
}