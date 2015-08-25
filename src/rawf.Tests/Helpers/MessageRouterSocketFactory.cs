using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Sockets;
using rawf.Tests.Backend.Setup;

namespace rawf.Tests.Helpers
{
    public class MessageRouterSocketFactory
    {
        private readonly RouterConfiguration config;
        private readonly IList<StubSocket> sockets;
        private readonly TimeSpan socketWaitTimeout;
        private readonly int socketWaitRetries = 5;

        public MessageRouterSocketFactory(RouterConfiguration config)
        {
            this.config = config;
            sockets = new List<StubSocket>();
            socketWaitTimeout = TimeSpan.FromSeconds(2);
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

            Debug.WriteLineIf(socket == null, $"Socket not found. Total router sockets:{sockets.Count}");

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

            Debug.WriteLineIf(socket == null, $"Socket not found. Total router sockets:{sockets.Count}");

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

            Debug.WriteLineIf(socket == null, $"Socket not found. Total router sockets:{sockets.Count}");
            
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