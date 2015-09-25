using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using kino.Sockets;
using kino.Tests.Actors.Setup;

namespace kino.Tests.Helpers
{
    public class IntercomMessageHubSocketFactory
    {
        private readonly ConcurrentBag<StubSocket> sockets;
        private readonly TimeSpan socketWaitTimeout;
        private readonly int socketWaitRetries = 5;

        public IntercomMessageHubSocketFactory()
        {
            sockets = new ConcurrentBag<StubSocket>();
            socketWaitTimeout = TimeSpan.FromSeconds(5);
        }

        public ISocket CreateSocket()
        {
            var socket = new StubSocket();
            sockets.Add(socket);

            return socket;
        }

        public ISocket CreateSubscriberSocket()
        {
            var socket = new StubSocket();
            sockets.Add(socket);

            return socket;
        }

        public StubSocket GetReceivingSocket()
        {
            StubSocket socket = null;
            var retries = socketWaitRetries;
            Func<bool> socketIsMissing = () => socket == null;

            while (retries-- > 0 && socketIsMissing())
            {
                socket = sockets.FirstOrDefault(s => s.GetIdentity() != null);
                Wait(socketIsMissing);
            }
            return socket;
        }

        public StubSocket GetSendingSocket()
        {
            StubSocket socket = null;
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
                Thread.Sleep(socketWaitTimeout);
            }
        }
    }
}