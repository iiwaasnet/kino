using System;
using System.Threading;
using kino.Messaging;

namespace kino.Connectivity.Kafka
{
    public interface IListener : IDisposable
    {
        IMessage Receive(CancellationToken token);
    }
}