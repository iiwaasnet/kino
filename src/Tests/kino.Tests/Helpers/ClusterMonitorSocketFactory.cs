using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using kino.Sockets;
using kino.Tests.Actors.Setup;

namespace kino.Tests.Helpers
{
    public class ClusterMonitorSocketFactory
    {
        private readonly ConcurrentBag<SocketMeta> sockets;
        private readonly TimeSpan socketWaitTimeout;
        private readonly int socketWaitRetries = 5;
        private const string CreateClusterMonitorSubscriptionSocketMethod = "CreateClusterMonitorSubscriptionSocket";
        private const string CreateClusterMonitorSendingSocketMethod = "CreateClusterMonitorSendingSocket";
        private const string CreateRouterCommunicationSocketMethod = "CreateRouterCommunicationSocket";

        public ClusterMonitorSocketFactory()
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
                            Purpose = GetSocketPurpose()
                        }
                );

            return socket;
        }

        private SocketPurpose GetSocketPurpose()
        {
            var stackFrames = new StackTrace(1).GetFrames();
            if (stackFrames
                    .FirstOrDefault(sf => sf.GetMethod().Name == CreateClusterMonitorSendingSocketMethod) != null)
            {
                return SocketPurpose.ClusterMonitorSendingSocket;
            }
            if (stackFrames
                    .FirstOrDefault(sf => sf.GetMethod().Name == CreateClusterMonitorSubscriptionSocketMethod) != null)
            {
                return SocketPurpose.ClusterMonitorSubscriptionSocket;
            }
            if (stackFrames
                    .FirstOrDefault(sf => sf.GetMethod().Name == CreateRouterCommunicationSocketMethod) != null)
            {
                return SocketPurpose.RouterCommunicationSocket;
            }

            throw new Exception($"Socket creation method is unexpected: {string.Join(" @ ", stackFrames.Select(sf => sf.ToString()))}");
        }

        private StubSocket GetSocket(SocketPurpose socketPurpose)
        {
            StubSocket socket = null;
            var retries = socketWaitRetries;
            Func<bool> socketIsMissing = () => socket == null;

            while (retries-- > 0 && socketIsMissing())
            {
                socket = sockets.FirstOrDefault(s => s.Purpose == socketPurpose)?.Socket;
                Wait(socketIsMissing);
            }
            return socket;
        }

        public StubSocket GetClusterMonitorSubscriptionSocket()
        {
            return GetSocket(SocketPurpose.ClusterMonitorSubscriptionSocket);
        }

        public StubSocket GetRouterCommunicationSocket()
        {
            return GetSocket(SocketPurpose.RouterCommunicationSocket);
        }

        public StubSocket GetClusterMonitorSendingSocket()
        {
            return GetSocket(SocketPurpose.ClusterMonitorSendingSocket);
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
            internal SocketPurpose Purpose { get; set; }
        }

        private enum SocketPurpose
        {
            ClusterMonitorSubscriptionSocket,
            ClusterMonitorSendingSocket,
            RouterCommunicationSocket
        }
    }
}