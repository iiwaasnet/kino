using System;
using System.Threading;
using NetMQ;
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

        public void Connect(string address)
        {
            socket.Connect(address);
        }

        public void Bind(string address)
        {
            socket.Bind(address);
        }


        public void Disconnect(string address)
        {
            socket.Disconnect(address);
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