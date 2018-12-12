using System;
using kino.Core.Framework;

namespace kino.Rendezvous.Configuration
{
    public class RendezvousConfigurationProvider : IRendezvousConfigurationProvider
    {
        public RendezvousConfigurationProvider(RendezvousConfiguration config)
        {
            HeartBeatInterval = config.HeartBeatInterval;
            BroadcastUri = config.BroadcastUri.ParseAddress().ToSocketAddress();
            UnicastUri = config.UnicastUri.ParseAddress().ToSocketAddress();
            PartnerBroadcastUri = config.PartnerBroadcastUri.ParseAddress().ToSocketAddress();
        }

        public string BroadcastUri { get; }

        public string UnicastUri { get; }

        public string PartnerBroadcastUri { get; }

        public TimeSpan HeartBeatInterval { get; }
    }
}