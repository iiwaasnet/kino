using System;

namespace kino.Rendezvous.Configuration
{
    public interface IRendezvousConfigurationProvider
    {
        string BroadcastUri { get; }

        string UnicastUri { get; }

        TimeSpan HeartBeatInterval { get; }
    }
}