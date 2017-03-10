using System;

namespace kino.Rendezvous.Configuration
{
    public interface IRendezvousConfigurationProvider
    {
        Uri BroadcastUri { get; }

        Uri UnicastUri { get; }

        TimeSpan HeartBeatInterval { get; }
    }
}