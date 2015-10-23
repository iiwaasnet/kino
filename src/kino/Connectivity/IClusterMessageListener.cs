using System;
using System.Threading;

namespace kino.Connectivity
{
    public interface IClusterMessageListener
    {
        void StartBlockingListenMessages(Action restartRequestHandler, CancellationToken token, Barrier gateway);
    }
}