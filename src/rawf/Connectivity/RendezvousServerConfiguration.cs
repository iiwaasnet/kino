using System;

namespace rawf.Connectivity
{
    public class RendezvousServerConfiguration
    {
        public Uri BroadcastEndpoint { get; set; }
        public SocketEndpoint UnicastEndpoint { get; set; }
    }
}