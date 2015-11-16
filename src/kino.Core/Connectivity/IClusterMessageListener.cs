using System;
using System.Threading;

namespace kino.Core.Connectivity
{
    public interface IClusterMessageListener
    {
        void StartBlockingListenMessages(Action restartRequestHandler, CancellationToken token, Barrier gateway);
    }
}