using System;

namespace rawf.Backend
{
    public interface IExternalRoutingTable
    {
        void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier, Uri uri);
        SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier);
        void RemoveRoute(SocketIdentifier socketIdentifier);
    }
}