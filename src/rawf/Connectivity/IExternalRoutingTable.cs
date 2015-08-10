using System;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public interface IExternalRoutingTable
    {
        void AddRoute(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier, Uri uri);
        ISocket GetRoute(MessageHandlerIdentifier messageHandlerIdentifier);
        void RemoveRoute(SocketIdentifier socketIdentifier);
    }
}