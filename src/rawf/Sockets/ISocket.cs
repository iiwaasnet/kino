using System;
using System.Threading;
using rawf.Messaging;

namespace rawf.Sockets
{
    public interface ISocket : IDisposable
    {
        void SendMessage(IMessage message);
        IMessage ReceiveMessage(CancellationToken cancellationToken);
        void Connect(Uri address);
        void Bind(Uri address);
        void Disconnect(Uri address);
        void Unbind(Uri address);
        void SetIdentity(byte[] identity);
        void SetMandatoryRouting(bool mandatory = true);
        byte[] GetIdentity();
        void Subscribe(string topic = "");
    }
}