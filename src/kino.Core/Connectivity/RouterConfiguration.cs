using System;

namespace kino.Core.Connectivity
{
    public class RouterConfiguration
    {
        public int ScaleOutReceiveMessageQueueLength { get; set; }

        public TimeSpan ConnectionEstablishWaitTime { get; set; }
    }    
}