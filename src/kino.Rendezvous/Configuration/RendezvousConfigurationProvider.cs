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
            UnicastUri = config.UnicastUri.ParseAddress().ToString();
        }

        public string BroadcastUri { get; }

        public string UnicastUri { get; }

        public TimeSpan HeartBeatInterval { get; }
    }
}