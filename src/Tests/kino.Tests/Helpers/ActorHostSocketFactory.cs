using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using kino.Connectivity;
using kino.Core.Framework;
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
            var socket = new MockSocket();
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

        public MockSocket GetRoutableSocket()
        {
            MockSocket socket = null;
            var retries = socketWaitRetries;
            Func<bool> socketIsMissing = () => socket == null;

            while (retries-- > 0 && socketIsMissing())
            {
                socket = sockets.FirstOrDefault(s => s.Socket.GetIdentity() != null)?.Socket;
                Wait(socketIsMissing);
            }
            return socket;
        }

        public MockSocket GetRegistrationSocket()
        {
            MockSocket socket = null;
            var retries = socketWaitRetries;
            Func<bool> socketIsMissing = () => socket == null;

            while (retries-- > 0 && socketIsMissing())
            {
                socket = sockets.FirstOrDefault(s => s.IsRegistrationSocket)?.Socket;
                Wait(socketIsMissing);
            }
            return socket;
        }

        public MockSocket GetAsyncCompletionSocket()
        {
            MockSocket socket = null;
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
                socketWaitTimeout.Sleep();
            }
        }

        private class SocketMeta
        {
            internal MockSocket Socket { get; set; }

            internal bool IsRegistrationSocket { get; set; }
        }
    }
}