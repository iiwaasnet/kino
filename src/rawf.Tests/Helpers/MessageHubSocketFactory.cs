using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using rawf.Sockets;
using rawf.Tests.Backend.Setup;

namespace rawf.Tests.Helpers
{
    public class MessageHubSocketFactory
    {
        private readonly IList<StubSocket> sockets;
        private readonly TimeSpan socketWaitTimeout;
        private readonly int socketWaitRetries = 5;

        public MessageHubSocketFactory()
        {
            sockets = new List<StubSocket>();
            socketWaitTimeout = TimeSpan.FromSeconds(5);
        }

        public ISocket CreateSocket()
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