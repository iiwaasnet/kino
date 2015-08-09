using System;
using System.Collections.Concurrent;
using System.Threading;
using NetMQ;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Sockets
{
    internal class Socket : ISocket
    {
        private readonly NetMQSocket socket;
        private static readonly TimeSpan ReceiveWaitTimeout;
        private readonly ConcurrentQueue<SocketStateChangeRequest> stateChangeRequests;

        static Socket()
        {
            ReceiveWaitTimeout = TimeSpan.FromSeconds(3);
        }

        public Socket(NetMQSocket socket)
        {
            stateChangeRequests = new ConcurrentQueue<SocketStateChangeRequest>();
            socket.Options.Linger = TimeSpan.Zero;
            socket.Options.ReconnectInterval = TimeSpan.FromMilliseconds(-1);
            socket.Options.TcpKeepalive = false;
            this.socket = socket;
        }

        public void SendMessage(IMessage message)
        {
            ProcessPendingStatusChangeRequests();

            var multipart = new MultipartMessage(message);
            socket.SendMessage(new NetMQMessage(multipart.Frames));
        }

        public IMessage ReceiveMessage(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ProcessPendingStatusChangeRequests();

                var message = socket.ReceiveMessage(ReceiveWaitTimeout);
                if (message != null)
                {
                    var multipart = new MultipartMessage(message);
                    return new Message(multipart);
                }
            }

            return null;
        }

        private void ProcessPendingStatusChangeRequests()
        {
            SocketStateChangeRequest request;

            while (stateChangeRequests.TryDequeue(out request))
            {
                switch (request.StateChange)
                {
                    case SocketStateChangeKind.Connect:
                        Connect(((SocketConnectionStateChanged) request).Endpoint);
                        return;
                    case SocketStateChangeKind.Disconnect:
                        Disconnect(((SocketConnectionStateChanged) request).Endpoint);
                        return;
                    case SocketStateChangeKind.Subscribe:
                        Subscribe(((SocketSubscriptionStateChanged) request).Topic);
                        return;
                    case SocketStateChangeKind.Unsubscribe:
                        Unsubscribe(((SocketSubscriptionStateChanged) request).Topic);
                        return;
                }
            }
        }

        public void EnqueueDisconnect(Uri address)
            => stateChangeRequests.Enqueue(new SocketConnectionStateChanged
                                           {
                                               Endpoint = address,
                                               StateChange = SocketStateChangeKind.Disconnect
                                           });

        public void EnqueueConnect(Uri address)
            => stateChangeRequests.Enqueue(new SocketConnectionStateChanged
                                           {
                                               Endpoint = address,
                                               StateChange = SocketStateChangeKind.Connect
                                           });

        public void EnqueueSubscribe(string topic = "")
            => stateChangeRequests.Enqueue(new SocketSubscriptionStateChanged
                                           {
                                               Topic = topic,
                                               StateChange = SocketStateChangeKind.Subscribe
                                           });

        public void EnqueueUnsubscribe(string topic = "")
            => stateChangeRequests.Enqueue(new SocketSubscriptionStateChanged
                                           {
                                               Topic = topic,
                                               StateChange = SocketStateChangeKind.Unsubscribe
                                           });

        public void Connect(Uri address)
            => socket.Connect(address.ToSocketAddress());

        public void Disconnect(Uri address)
            => socket.Disconnect(address.ToSocketAddress());

        public void Bind(Uri address)
            => socket.Bind(address.ToSocketAddress());

        public void Unbind(Uri address)
            => socket.Unbind(address.ToSocketAddress());

        public void Subscribe(string topic = "")
            => socket.Subscribe(topic);

        public void Unsubscribe(string topic = "")
            => socket.Unsubscribe(topic);

        public void SetMandatoryRouting(bool mandatory = true)
            => socket.Options.RouterMandatory = mandatory;

        public void SetIdentity(byte[] identity)
            => socket.Options.Identity = identity;

        public byte[] GetIdentity()
            => socket.Options.Identity;

        public void Dispose()
            => socket.Dispose();
    }
}