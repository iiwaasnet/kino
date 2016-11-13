using System;
using System.Collections.Concurrent;
using System.Linq;
using kino.Connectivity;
using kino.Core.Framework;
using kino.Tests.Actors.Setup;

namespace kino.Tests.Helpers
{
    public class MessageHubSocketFactory
    {
        private readonly ConcurrentBag<MockSocket> sockets;
        private readonly TimeSpan socketWaitTimeout;
        private readonly int socketWaitRetries = 5;

        public MessageHubSocketFactory()
        {
            sockets = new ConcurrentBag<MockSocket>();
            socketWaitTimeout = TimeSpan.FromSeconds(5);
        }

        public ISocket CreateSocket()
        {
            var socket = new MockSocket();
            sockets.Add(socket);

            return socket;
        }

        public MockSocket GetReceivingSocket()
        {
            MockSocket socket = null;
            var retries = socketWaitRetries;
            Func<bool> socketIsMissing = () => socket == null;

            while (retries-- > 0 && socketIsMissing())
            {
                socket = sockets.FirstOrDefault(s => s.GetIdentity() != null);
                Wait(socketIsMissing);
            }
            return socket;
        }

        public MockSocket GetSendingSocket()
        {
            MockSocket socket = null;
            var retries = socketWaitRetries;
            Func<bool> socketIsMissing = () => socket == null;

            while (retries-- > 0 && socketIsMissing())
            {
                socket = sockets.FirstOrDefault(s => s.GetIdentity() == null);
                Wait(socketIsMissing);
            }
            return socket;
        }

        private void Wait(Func<bool> waitIf)
        {
            if (waitIf())
            {
                socketWaitTimeout.Sleep();
            }
        }
    }
}