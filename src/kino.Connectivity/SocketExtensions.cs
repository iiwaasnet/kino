using System;
using NetMQ;

namespace kino.Connectivity
{
    public static class SocketExtensions
    {
        public static void SafeDisconnect(this ISocket socket, string uri)
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