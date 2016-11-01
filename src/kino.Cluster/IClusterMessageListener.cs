using System;
using System.Threading;

namespace kino.Cluster
{
    public interface IClusterMessageListener
    {
        void StartBlockingListenMessages(Action restartRequestHandler, CancellationToken token, Barrier gateway);
    }
}