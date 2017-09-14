using System;

namespace kino.Connectivity
{
    public class SocketConfiguration
    {
        public TimeSpan Linger { get; set; }

        public int SendingHighWatermark { get; set; }

        public int ReceivingHighWatermark { get; set; }

        public TimeSpan SendTimeout { get; set; }

        public TimeSpan ReceiveWaitTimeout { get; set; }

        public TimeSpan ConnectionEstablishmentTime { get; set; }
    }
}