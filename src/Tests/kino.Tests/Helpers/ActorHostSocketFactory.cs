using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using kino.Sockets;
using kino.Tests.Actors.Setup;

namespace kino.Tests.Helpers
{
    public class ActorHostSocketFactory
    {
        private readonly ConcurrentBag<SocketMeta> sockets;
        private readonly TimeSpan socketWaitTimeout;
        private readonly int socketWaitRetries = 5;
        private const string ActorRegistrationMethod = "RegisterActors";

        public ActorHostSocketFactory()
        {
            sockets = new ConcurrentBag<SocketMeta>();
            socketWaitTimeout = TimeSpan.FromSeconds(5);
        }

        public ISocket CreateSocket()
        {
            var socket = new StubSocket();
            sockets.Add(new SocketMeta
                        {
                            Socket = socket,
                            IsRegistrationSocket = IsRegistrationSocket()
                        }
                );

            return socket;
        }

        private bool IsRegistrationSocket()
        {
            return new StackTrace(1)
                       .GetFrames()
                       .FirstOrDefault(sf => sf.GetMethod().Name == ActorRegistrationMethod) != null;
        }

        public StubSocket GetRoutableSocket()
        {
            StubSocket socket = null;
            var retries = socketWaitRetries;
            Func<bool> socketIsMissing = () => socket == null;

            while (retries-- > 0 && socketIsMissing())
            {
                socket = sockets.FirstOrDefault(s => s.Socket.GetIdentity() != null)?.Socket;
                Wait(socketIsMissing);
            }
            return socket;
        }

        public StubSocket GetRegistrationSocket()
        {
            StubSocket socket = null;
            var retries = socketWaitRetries;
            Func<bool> socketIsMissing = () => socket == null;

            while (retries-- > 0 && socketIsMissing())
            {
                socket = sockets.FirstOrDefault(s => s.IsRegistrationSocket)?.Socket;
                Wait(socketIsMissing);
            }
            return socket;
        }

        public StubSocket GetAsyncCompletionSocket()
        {
            StubSocket socket = null;
            var retries = socketWaitRetries;
            Func<bool> socketIsMissing = () => socket == null;

            while (retries-- > 0 && socketIsMissing())
            {
                socket = sockets.FirstOrDefault(s => !s.IsRegistrationSocket && s.Socket.GetIdentity() == null)?.Socket;
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

        private class SocketMeta
        {
            internal StubSocket Socket { get; set; }
            internal bool IsRegistrationSocket { get; set; }
        }
    }
}