using rawf.Consensus;

namespace rawf.Rendezvous
{
    public class LeaseConfigurationProvider : ILeaseConfigurationProvider
    {
        private readonly ILeaseConfiguration config;

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

        public ILeaseConfiguration GetConfiguration()
            => config;
    }
}