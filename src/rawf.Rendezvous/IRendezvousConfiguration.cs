using System;

namespace rawf.Rendezvous
{
    public interface IRendezvousConfiguration
    {
        Uri MulticastUri { get; }
        Uri UnicastUri { get; }
        TimeSpan PingInterval { get; }
    }
}