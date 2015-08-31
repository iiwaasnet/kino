using System;

namespace kino.Rendezvous
{
    public class RendezvousConfigurationProvider : IRendezvousConfigurationProvider
    {
        private readonly RendezvousConfiguration config;

        public RendezvousConfigurationProvider(ApplicationConfiguration appConfig)
        {
            config = new RendezvousConfiguration
                     {
                         MulticastUri = new Uri(appConfig.BroadcastUri),
                         UnicastUri = new Uri(appConfig.UnicastUri),
                         PingInterval = appConfig.PingInterval
                     };
        }

        public RendezvousConfiguration GetConfiguration()
            => config;
    }
}