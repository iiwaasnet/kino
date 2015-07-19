using System;
using rawf.Actors;
using rawf.Framework;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public class ConnectivityProvider : IConnectivityProvider
    {
        private readonly ISocketProvider socketProvider;
        private readonly IConnectivityConfiguration connectivityConfiguration;
        private static readonly byte[] RouterIdentity = {0, 1, 2, 3};
        private static readonly byte[] ScaleOutSocketIdentity = {4, 5, 6, 7};

        public ConnectivityProvider(ISocketProvider socketProvider, IConnectivityConfiguration connectivityConfiguration)
        {
            this.socketProvider = socketProvider;
            this.connectivityConfiguration = connectivityConfiguration;
        }

        public ISocket CreateRouterSocket()
        {
            var socket = socketProvider.CreateRouterSocket();
            socket.SetMandatoryRouting();
            socket.SetIdentity(RouterIdentity);
            socket.Bind(connectivityConfiguration.GetRouterAddress());

            return socket;
        }

        public ISocket CreateFrontendScaleOutSocket()
        {
            var socket = socketProvider.CreateRouterSocket();
            socket.SetIdentity(ScaleOutSocketIdentity);
            socket.SetMandatoryRouting();
            socket.Connect(connectivityConfiguration.GetRouterAddress());
            socket.Bind(connectivityConfiguration.GetLocalScaleOutAddress());

            return socket;
        }

        public ISocket CreateBackendScaleOutSocket()
        {
            var socket = socketProvider.CreateRouterSocket();
            socket.SetIdentity(ScaleOutSocketIdentity);
            socket.SetMandatoryRouting();
            foreach (var peer in connectivityConfiguration.GetScaleOutCluster())
            {
                socket.Connect(peer);
            }

            return socket;
        }

        public ISocket CreateActorSyncSocket()
        {
            var socket = socketProvider.CreateDealerSocket();
            socket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            socket.Connect(connectivityConfiguration.GetRouterAddress());

            return socket;
        }

        public ISocket CreateActorAsyncSocket()
        {
            var socket = socketProvider.CreateDealerSocket();
            socket.Connect(connectivityConfiguration.GetRouterAddress());

            return socket;
        }

        public ISocket CreateClientSendingSocket()
        {
            var socket = socketProvider.CreateDealerSocket();
            socket.Connect(connectivityConfiguration.GetRouterAddress());

            return socket;
        }

        public ISocket CreateClientReceivingSocket()
        {
            var socket = socketProvider.CreateDealerSocket();
            socket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            socket.Connect(connectivityConfiguration.GetRouterAddress());

            return socket;
        }
    }
}