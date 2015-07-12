using NetMQ;
using rawf.Messaging;

namespace rawf.Sockets
{
    internal class Socket : ISocket
    {
        private readonly NetMQSocket socket;

        public Socket(NetMQSocket socket)
        {
            this.socket = socket;
        }

        public void SendMessage(IMessage message)
        {
            var multipart = new MultipartMessage(message);
            socket.SendMessage(new NetMQMessage(multipart.Frames));
        }

        public IMessage ReceiveMessage()
        {
            var message = socket.ReceiveMessage();
            var multipart = new MultipartMessage(message);

            return new Message(multipart);
        }

        public void Connect(string address)
        {
            socket.Connect(address);
        }

        public void Disconnect(string address)
        {
            socket.Disconnect(address);
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