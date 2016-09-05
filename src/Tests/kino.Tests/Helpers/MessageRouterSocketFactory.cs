using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using kino.Core.Connectivity;
using kino.Core.Framework;
using kino.Core.Sockets;
using kino.Tests.Actors.Setup;

namespace kino.Tests.Helpers
{
    public class MessageRouterSocketFactory
    {
        private readonly RouterConfiguration config;
        private readonly SocketEndpoint scaleOutAddress;
        private readonly ConcurrentBag<MockSocket> sockets;
        private readonly TimeSpan socketWaitTimeout;
        private readonly int socketWaitRetries = 5;

        public MessageRouterSocketFactory(RouterConfiguration config, SocketEndpoint scaleOutAddress)
        {
            this.config = config;
            this.scaleOutAddress = scaleOutAddress;
            sockets = new ConcurrentBag<MockSocket>();
            socketWaitTimeout = TimeSpan.FromSeconds(5);
        }

        public ISocket CreateSocket()
        {
            var socket = new MockSocket();
            sockets.Add(socket);

            return socket;
        }

        public MockSocket GetRouterSocket()
        {
            MockSocket socket = null;
            var retries = socketWaitRetries;

            while (retries-- > 0 && socket == null)
            {
                socket = sockets.FirstOrDefault(s => s != null && Unsafe.Equals(s.GetIdentity(), config.RouterAddress.Identity));
                Wait(socket);
            }

            return socket;
        }

        public MockSocket GetScaleoutFrontendSocket()
        {
            MockSocket socket = null;
            var retries = socketWaitRetries;

            while (retries-- > 0 && socket == null)
            {
                socket = sockets.FirstOrDefault(s => s != null && Unsafe.Equals(s.GetIdentity(), scaleOutAddress.Identity));
                Wait(socket);
            }

            return socket;
        }

        public MockSocket GetScaleoutBackendSocket()
        {
            MockSocket socket = null;
            var retries = socketWaitRetries;

            while (retries-- > 0 && socket == null)
            {
                socket = sockets.FirstOrDefault(s => s.GetIdentity() == null);
                Wait(socket);
            }

            return socket;
        }

        private void Wait(MockSocket socket)
        {
            if (socket == null)
            {
                Thread.Sleep(socketWaitTimeout);
            }
        }
    }
}