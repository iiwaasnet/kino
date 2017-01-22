using kino.Connectivity;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Routing.ServiceMessageHandlers
{
    public class PingHandler : IServiceMessageHandler
    {
        public void Handle(IMessage message, ISocket _)
        {
        }

        public MessageIdentifier TargetMessage => KinoMessages.Ping;
    }
}