using kino.Core.Messaging;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public interface IServiceMessageHandler
    {
        bool Handle(IMessage message, ISocket forwardingSocket);
    }
}