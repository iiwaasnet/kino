using kino.Messaging;
using kino.Sockets;

namespace kino.Connectivity.ServiceMessageHandlers
{
    public interface IServiceMessageHandler
    {
        bool Handle(IMessage message, ISocket scaleOutBackendSocket);
    }
}