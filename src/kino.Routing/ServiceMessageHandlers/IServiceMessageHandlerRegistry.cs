using kino.Core;

namespace kino.Routing.ServiceMessageHandlers
{
    public interface IServiceMessageHandlerRegistry
    {
        IServiceMessageHandler GetMessageHandler(MessageIdentifier message);
    }
}