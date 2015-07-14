using System;
using System.Threading;
using rawf.Messaging;

namespace rawf.Sockets
{
    public interface ISocket : IDisposable
    {
        void SendMessage(IMessage message);
        IMessage ReceiveMessage(CancellationToken cancellationToken);
        void Connect(string address);
        void Disconnect(string address);
        void SetIdentity(byte[] identity);
        byte[] GetIdentity();
    }
}