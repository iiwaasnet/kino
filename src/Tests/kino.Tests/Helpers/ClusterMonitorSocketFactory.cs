using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using kino.Connectivity;
using kino.Core.Framework;
using kino.Tests.Actors.Setup;

namespace kino.Tests.Helpers
{
    public class ClusterMonitorSocketFactory
    {
        private readonly ConcurrentBag<SocketMeta> sockets;
        private readonly TimeSpan socketWaitTimeout;
        private readonly int socketWaitRetries;
        private const string CreateClusterMonitorSubscriptionSocketMethod = "CreateClusterMonitorSubscriptionSocket";
        private const string CreateClusterMonitorSendingSocketMethod = "CreateClusterMonitorSendingSocket";
        private const string CreateRouterCommunicationSocketMethod = "CreateRouterCommunicationSocket";

        public ClusterMonitorSocketFactory()
        {
            sockets = new ConcurrentBag<SocketMeta>();
            socketWaitTimeout = TimeSpan.FromMilliseconds(50);
            socketWaitRetries = 500;
        }

        public ISocket CreateSocket()
        {
            var socket = new MockSocket();
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
            if (stackFrames.FirstOrDefault(sf => sf.GetMethod().Name == CreateClusterMonitorSendingSocketMethod) != null)
            {
                return SocketPurpose.ClusterMonitorSendingSocket;
            }
            if (stackFrames.FirstOrDefault(sf => sf.GetMethod().Name == CreateClusterMonitorSubscriptionSocketMethod) != null)
            {
                return SocketPurpose.ClusterMonitorSubscriptionSocket;
            }
            if (stackFrames.FirstOrDefault(sf => sf.GetMethod().Name == CreateRouterCommunicationSocketMethod) != null)
            {
                return SocketPurpose.RouterCommunicationSocket;
            }

            throw new Exception($"Socket creation method is unexpected: {string.Join(" @ ", stackFrames.Select(sf => sf.ToString()))}");
        }

        private MockSocket GetSocket(SocketPurpose socketPurpose)
        {
            MockSocket socket = null;
            var retries = socketWaitRetries;
            Func<bool> socketIsMissing = () => socket == null;

            while (retries-- > 0 && socketIsMissing())
            {
                socket = sockets.FirstOrDefault(s => s.Purpose == socketPurpose)?.Socket;
                Wait(socketIsMissing);
            }
            return socket;
        }

        public MockSocket GetClusterMonitorSubscriptionSocket()
        {
            return GetSocket(SocketPurpose.ClusterMonitorSubscriptionSocket);
        }

        public MockSocket GetRouterCommunicationSocket()
        {
            return GetSocket(SocketPurpose.RouterCommunicationSocket);
        }

        public MockSocket GetClusterMonitorSendingSocket()
        {
            return GetSocket(SocketPurpose.ClusterMonitorSendingSocket);
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