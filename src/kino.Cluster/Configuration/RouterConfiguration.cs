using System;

namespace kino.Cluster.Configuration
{
    public class RouterConfiguration
    {
        public int ScaleOutReceiveMessageQueueLength { get; set; }

        public TimeSpan ConnectionEstablishWaitTime { get; set; }
    }    
}