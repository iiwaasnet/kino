using System;

namespace kino.Sockets
{
    public class SocketConfiguration
    {
        public TimeSpan Linger { get; set; }
        public int SendingHighWatermark { get; set; }
        public int ReceivingHighWatermark { get; set; }
    }
}