using System;
using NetMQ;

namespace kino.Core.Sockets
{
    public static class SocketExtensions
    {
        public static void SafeDisconnect(this ISocket socket, Uri uri)
        {
            try
            {
                socket.Disconnect(uri);
            }
            catch (EndpointNotFoundException)
            {
            }
        }
    }
}