using kino.Connectivity;
using kino.Core;
using kino.Messaging;

namespace kino.Routing.ServiceMessageHandlers
{
    public interface IServiceMessageHandler
    {
        void Handle(IMessage message, ISocket scaleOutBackend);

        MessageIdentifier TargetMessage { get; }
    }
}