using kino.Connectivity;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Routing.ServiceMessageHandlers
{
    public class PingHandler : IServiceMessageHandler
    {
        public bool Handle(IMessage message, ISocket _)
            => message.Equals(KinoMessages.Ping);
    }
}