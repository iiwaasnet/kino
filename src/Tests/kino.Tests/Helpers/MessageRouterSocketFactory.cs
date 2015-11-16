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
        private readonly ConcurrentBag<StubSocket> sockets;
        private readonly TimeSpan socketWaitTimeout;
        private readonly int socketWaitRetries = 5;

        public MessageRouterSocketFactory(RouterConfiguration config)
        {
            this.config = config;
            sockets = new ConcurrentBag<StubSocket>();
            socketWaitTimeout = TimeSpan.FromSeconds(5);
        }

        public ISocket CreateSocket()
        {
            var socket = new StubSocket();
            sockets.Add(socket);

            return socket;
        }

        public StubSocket GetRouterSocket()
        {
            StubSocket socket = null;
            var retries = socketWaitRetries;

            while (retries-- > 0 && socket == null)
            {
                socket = sockets.FirstOrDefault(s => s != null && Unsafe.Equals(s.GetIdentity(), config.RouterAddress.Identity));
                Wait(socket);
            }

            return socket;
        }

        public StubSocket GetScaleoutFrontendSocket()
        {
            StubSocket socket = null;
            var retries = socketWaitRetries;

            while (retries-- > 0 && socket == null)
            {
                socket = sockets.FirstOrDefault(s => s != null && Unsafe.Equals(s.GetIdentity(), config.ScaleOutAddress.Identity));
                Wait(socket);
            }

            return socket;
        }

        public StubSocket GetScaleoutBackendSocket()
        {
            StubSocket socket = null;
            var retries = socketWaitRetries;

            while (retries-- > 0 && socket == null)
            {
                socket = sockets.FirstOrDefault(s => s.GetIdentity() == null);
                Wait(socket);
            }

            return socket;
        }

        private void Wait(StubSocket socket)
        {
            if (socket == null)
            {
                Thread.Sleep(socketWaitTimeout);
            }
        }
    }
}