using System;
using kino.Messaging;

namespace kino.Connectivity.Kafka
{
    public interface ISender : IDisposable
    {
        void Send(string brokerName, string destination, IMessage message);

        void Connect(string brokerName);

        void Disconnect(string brokerName);
    }
}