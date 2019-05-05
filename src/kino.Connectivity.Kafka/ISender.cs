using System;
using kino.Messaging;

namespace kino.Connectivity.Kafka
{
    public interface ISender : IDisposable
    {
        void Send(string destination, IMessage message);
    }
}