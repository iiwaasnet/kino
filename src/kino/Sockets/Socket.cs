using System;
using System.Threading;
using kino.Framework;
using kino.Messaging;
using NetMQ;

namespace kino.Sockets
{
    internal class Socket : ISocket
    {
        private readonly NetMQSocket socket;
        private static readonly TimeSpan ReceiveWaitTimeout;

        static Socket()
        {
            ReceiveWaitTimeout = TimeSpan.FromSeconds(3);
        }

        internal Socket(NetMQSocket socket, SocketConfiguration config)
        {
            socket.Options.Linger = config.Linger;
            socket.Options.ReceiveHighWatermark = config.ReceivingHighWatermark;
            socket.Options.SendHighWatermark = config.SendingHighWatermark;
            this.socket = socket;
        }

        public void SendMessage(IMessage message)
        {
            var multipart = new MultipartMessage((Message) message);
            socket.SendMultipartMessage(new NetMQMessage(multipart.Frames));
        }

        public IMessage ReceiveMessage(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = new NetMQMessage();
                if (socket.TryReceiveMultipartMessage(ReceiveWaitTimeout, ref message))
                {
                    var multipart = new MultipartMessage(message);
                    return new Message(multipart);
                }
            }

            return null;
        }

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

        public void Subscribe(byte[] topic)
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