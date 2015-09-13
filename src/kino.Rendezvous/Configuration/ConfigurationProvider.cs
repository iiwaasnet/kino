using System;
using kino.Rendezvous.Consensus;

namespace kino.Rendezvous.Configuration
{
    public class ConfigurationProvider : IConfigurationProvider
    {
        private readonly ApplicationConfiguration appConfig;

        public ConfigurationProvider(ApplicationConfiguration appConfig)
        {
            this.appConfig = appConfig;
        }

        public LeaseConfiguration GetLeaseConfiguration()
            => new LeaseConfiguration
               {
                   ClockDrift = appConfig.Synod.ClockDrift,
                   MaxLeaseTimeSpan = appConfig.Synod.MaxLeaseTimeSpan,
                   MessageRoundtrip = appConfig.Synod.MessageRoundtrip,
                   NodeResponseTimeout = appConfig.Synod.NodeResponseTimeout
               };

        public RendezvousConfiguration GetRendezvousConfiguration()
            => new RendezvousConfiguration
               {
                   ServiceName = appConfig.ServiceName,
                   MulticastUri = new Uri(appConfig.BroadcastUri),
                   UnicastUri = new Uri(appConfig.UnicastUri),
                   PingInterval = appConfig.PingInterval
               };
    }
}