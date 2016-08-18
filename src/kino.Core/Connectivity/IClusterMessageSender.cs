using System.Threading;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public interface IClusterMessageSender
    {
        void StartBlockingSendMessages(CancellationToken token, Barrier gateway);

        void EnqueueMessage(IMessage message);
    }
}