using System;
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

        static Socket()
        {
            ReceiveWaitTimeout = TimeSpan.FromSeconds(3);
        }

        public Socket(NetMQSocket socket)
        {
            this.socket = socket;
            this.socket.Options.Linger = TimeSpan.Zero;
        }

        public void SendMessage(IMessage message)
        {
            var multipart = new MultipartMessage(message);
            socket.SendMessage(new NetMQMessage(multipart.Frames));
        }

        public IMessage ReceiveMessage(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = socket.ReceiveMessage(ReceiveWaitTimeout);
                if (message != null)
                {
                    var multipart = new MultipartMessage(message);
                    return new Message(multipart);
                }
            }

            return null;
        }

        public void Connect(Uri address)
        {            
            socket.Connect(address.ToSocketAddress());
        }

        public void Disconnect(Uri address)
        {
            socket.Disconnect(address.ToSocketAddress());
        }

        public void Bind(Uri address)
        {
            socket.Bind(address.ToSocketAddress());
        }

        public void Unbind(Uri address)
        {
            socket.Unbind(address.ToSocketAddress());
        }

        public void Subscribe(string topic = "")
        {
            socket.Subscribe(topic);
        }

        public void SetMandatoryRouting(bool mandatory = true)
        {
            socket.Options.RouterMandatory = mandatory;
        }

        public void SetIdentity(byte[] identity)
        {
            socket.Options.Identity = identity;
        }

        public byte[] GetIdentity()
        {
            return socket.Options.Identity;
        }

        public void Dispose()
        {
            socket.Dispose();
        }
    }
}