using System;
using System.Collections.Concurrent;
using System.Linq;
using kino.Connectivity;
using kino.Core;
using kino.Core.Framework;
using kino.Tests.Actors.Setup;

namespace kino.Tests.Helpers
{
    public delegate void SocketCreationHandler(MockSocket socket);

    public class MessageRouterSocketFactory
    {
        private readonly SocketEndpoint scaleOutAddress;
        private readonly ConcurrentBag<MockSocket> sockets;
        private readonly TimeSpan socketWaitTimeout;
        private readonly int socketWaitRetries = 5;

        public MessageRouterSocketFactory(SocketEndpoint scaleOutAddress)
        {
            this.scaleOutAddress = scaleOutAddress;
            sockets = new ConcurrentBag<MockSocket>();
            socketWaitTimeout = TimeSpan.FromSeconds(5);
        }

        public ISocket CreateSocket()
        {
            var socket = new MockSocket();
            sockets.Add(socket);

            OnSocketCreated(socket);

            return socket;
        }

        //public MockSocket GetRouterSocket()
        //{
        //    MockSocket socket = null;
        //    var retries = socketWaitRetries;

        //    while (retries-- > 0 && socket == null)
        //    {
        //        socket = sockets.FirstOrDefault(IsRouterSocket);
        //        Wait(socket);
        //    }

        //    return socket;
        //}

        //private bool IsRouterSocket(MockSocket s)
        //    => Equals(s?.GetIdentity(), config.RouterAddress.Identity);

        public MockSocket GetScaleoutFrontendSocket()
        {
            MockSocket socket = null;
            var retries = socketWaitRetries;

            while (retries-- > 0 && socket == null)
            {
                socket = sockets.FirstOrDefault(IsScaleOutFrontendSocket);
                Wait(socket);
            }

            return socket;
        }

        private bool IsScaleOutFrontendSocket(MockSocket s)
            => Equals(s?.GetIdentity(), scaleOutAddress.Identity);

        public MockSocket GetScaleoutBackendSocket()
        {
            MockSocket socket = null;
            var retries = socketWaitRetries;

            while (retries-- > 0 && socket == null)
            {
                socket = sockets.FirstOrDefault(IsScaleOutBackendSocket);
                Wait(socket);
            }

            return socket;
        }

        private static bool IsScaleOutBackendSocket(MockSocket s)
            => s.GetIdentity() == null;

        private void Wait(MockSocket socket)
        {
            if (socket == null)
            {
                socketWaitTimeout.Sleep();
            }
        }

        private void OnSocketCreated(MockSocket socket)
            => SocketCreated?.Invoke(socket);

        public event SocketCreationHandler SocketCreated;
    }
}