﻿using System;

namespace kino.Core.Sockets
{
    public class SocketConfiguration
    {
        public TimeSpan Linger { get; set; }

        public int SendingHighWatermark { get; set; }

        public int ReceivingHighWatermark { get; set; }

        public TimeSpan SendTimeout { get; set; }
    }
}