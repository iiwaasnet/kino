using rawf.Rendezvous.Consensus;

namespace rawf.Rendezvous
{
    public class LeaseConfigurationProvider : ILeaseConfigurationProvider
    {
        private readonly LeaseConfiguration config;

        public LeaseConfigurationProvider(ApplicationConfiguration appConfig)
        {
            config = new LeaseConfiguration
                     {
                         ClockDrift = appConfig.Synod.ClockDrift,
                         MaxLeaseTimeSpan = appConfig.Synod.MaxLeaseTimeSpan,
                         MessageRoundtrip = appConfig.Synod.MessageRoundtrip,
                         NodeResponseTimeout = appConfig.Synod.NodeResponseTimeout
                     };
        }

        public LeaseConfiguration GetConfiguration()
            => config;
    }
}