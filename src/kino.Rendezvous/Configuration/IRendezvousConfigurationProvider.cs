using System;

namespace kino.Rendezvous.Configuration
{
    public interface IRendezvousConfigurationProvider
    {
        string BroadcastUri { get; }

        string PartnerBroadcastUri { get; }

        string UnicastUri { get; }

        TimeSpan HeartBeatInterval { get; }
    }
}