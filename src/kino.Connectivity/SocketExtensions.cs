using System;
using NetMQ;

namespace kino.Connectivity
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