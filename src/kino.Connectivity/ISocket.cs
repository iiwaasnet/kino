using System;
using System.Threading;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;

namespace kino.Connectivity
{
    public interface ISocket : IDisposable
    {
        void SendMessage(IMessage message);

        IMessage ReceiveMessage(CancellationToken cancellationToken);

        void Connect(Uri address, bool waitUntilConnected = false);

        void Bind(Uri address);

        void Disconnect(Uri address);

        void Unbind(Uri address);

        void SetIdentity(byte[] identity);

        void SetMandatoryRouting(bool mandatory = true);

        void SetReceiveHighWaterMark(int hwm);

        byte[] GetIdentity();

        void Subscribe(string topic = "");

        void Subscribe(byte[] topic);

        void Unsubscribe(string topic = "");

        IPerformanceCounter ReceiveRate { get; set; }

        IPerformanceCounter SendRate { get; set; }
    }
}