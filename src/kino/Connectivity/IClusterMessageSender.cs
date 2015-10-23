using System.Threading;
using kino.Messaging;

namespace kino.Connectivity
{
    public interface IClusterMessageSender
    {
        void StartBlockingSendMessages(CancellationToken token, Barrier gateway);
        void EnqueueMessage(IMessage message);
    }
}