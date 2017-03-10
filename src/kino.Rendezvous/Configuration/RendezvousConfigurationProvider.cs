using System;
using kino.Core.Framework;

namespace kino.Rendezvous.Configuration
{
    public class RendezvousConfigurationProvider : IRendezvousConfigurationProvider
    {
        public RendezvousConfigurationProvider(RendezvousConfiguration config)
        {
            HeartBeatInterval = config.HeartBeatInterval;
            BroadcastUri = config.BroadcastUri.ParseAddress();
            UnicastUri = config.UnicastUri.ParseAddress();
        }

        public Uri BroadcastUri { get; }

        public Uri UnicastUri { get; }

        public TimeSpan HeartBeatInterval { get; }
    }
}