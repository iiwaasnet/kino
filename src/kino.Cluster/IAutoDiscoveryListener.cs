using System;
using System.Threading;

namespace kino.Cluster
{
    public interface IAutoDiscoveryListener
    {
        void StartBlockingListenMessages(Action restartRequestHandler, CancellationToken token, Barrier gateway);
    }
}