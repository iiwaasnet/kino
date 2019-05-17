using System;
using System.Threading;
using kino.Messaging;

namespace kino.Connectivity.Kafka
{
    public interface IListener : IDisposable
    {
        IMessage Receive(CancellationToken token);

        void Subscribe(string brokerName, string topic);

        void Unsubscribe(string brokerName, string topic);

        void Connect(string brokerName);

        void Disconnect(string brokerName);

        void SetIdentity(byte[] identity);
    }
}