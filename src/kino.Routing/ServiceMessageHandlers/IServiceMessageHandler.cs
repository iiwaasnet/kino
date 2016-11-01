using kino.Connectivity;
using kino.Messaging;

namespace kino.Routing.ServiceMessageHandlers
{
    public interface IServiceMessageHandler
    {
        bool Handle(IMessage message, ISocket forwardingSocket);
    }
}