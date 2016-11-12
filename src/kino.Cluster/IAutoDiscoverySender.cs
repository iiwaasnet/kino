using System.Threading;
using kino.Messaging;

namespace kino.Cluster
{
    public interface IAutoDiscoverySender
    {
        void StartBlockingSendMessages(CancellationToken token, Barrier gateway);

        void EnqueueMessage(IMessage message);
    }
}