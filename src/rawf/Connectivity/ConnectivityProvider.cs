using System;
using System.Collections.Generic;
using System.Linq;
using rawf.Framework;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public class ConnectivityProvider : IConnectivityProvider
    {
        private readonly IClusterConfiguration clusterConfiguration;
        private readonly INodeConfiguration nodeConfiguration;
        private readonly ISocketFactory socketFactory;
        private readonly IRendezvousConfiguration rendezvousConfiguration;
        private readonly RendezvousServerConfiguration currentRendezvousServer;

        public ConnectivityProvider(ISocketFactory socketFactory,
                                    INodeConfiguration nodeConfiguration,
                                    IClusterConfiguration clusterConfiguration,
                                    IRendezvousConfiguration rendezvousConfiguration)
        {
            this.socketFactory = socketFactory;
            this.nodeConfiguration = nodeConfiguration;
            this.clusterConfiguration = clusterConfiguration;
            this.rendezvousConfiguration = rendezvousConfiguration;
            currentRendezvousServer = rendezvousConfiguration.GetRendezvousServers().First();
        }

        public ISocket CreateRouterSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetMandatoryRouting();
            socket.SetIdentity(nodeConfiguration.RouterAddress.Identity);
            socket.Bind(nodeConfiguration.RouterAddress.Uri);

            return socket;
        }

        public ISocket CreateScaleOutFrontendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetIdentity(nodeConfiguration.ScaleOutAddress.Identity);
            socket.SetMandatoryRouting();
            socket.Connect(nodeConfiguration.RouterAddress.Uri);
            socket.Bind(nodeConfiguration.ScaleOutAddress.Uri);

            return socket;
        }

        public ISocket CreateScaleOutBackendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetIdentity(nodeConfiguration.ScaleOutAddress.Identity);
            socket.SetMandatoryRouting();
            foreach (var peer in clusterConfiguration.GetClusterMembers())
            {
                socket.Connect(peer.Uri);
            }

            return socket;
        }

        public ISocket CreateActorSyncSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            socket.Connect(nodeConfiguration.RouterAddress.Uri);

            return socket;
        }

        public ISocket CreateActorAsyncSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(nodeConfiguration.RouterAddress.Uri);

            return socket;
        }

        public ISocket CreateMessageHubSendingSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(nodeConfiguration.RouterAddress.Uri);

            return socket;
        }

        public ISocket CreateMessageHubReceivingSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            socket.Connect(nodeConfiguration.RouterAddress.Uri);

            return socket;
        }

        public ISocket CreateRendezvousSendingSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SetIdentity(currentRendezvousServer.P2PEndpoint.Identity);
            socket.Connect(currentRendezvousServer.P2PEndpoint.Uri);

            return socket;
        }

        public ISocket CreateRendezvousSubscriptionSocket()
        {
            var socket = socketFactory.CreateSubscriberSocket();
            socket.Connect(currentRendezvousServer.BroadcastEndpoint);

            return socket;
        }

        public IEnumerable<NodeIdentity> GetClusterIdentities()
        {
            return clusterConfiguration.GetClusterMembers().Select(m => new NodeIdentity {Value = m.Identity});
        }
    }
}