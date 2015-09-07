using kino.Connectivity;

namespace Server
{
    public class ClusterTimingConfigurationProvider : IClusterTimingConfigurationProvider
    {
        private readonly ClusterTimingConfiguration config;

        public ClusterTimingConfigurationProvider(ApplicationConfiguration appConfig)
        {
            config = new ClusterTimingConfiguration
                     {
                         PongSilenceBeforeRouteDeletion = appConfig.PongSilenceBeforeRouteDeletion,
                         PingSilenceBeforeRendezvousFailover = appConfig.PingSilenceBeforeRendezvousFailover
                     };
        }

        public ClusterTimingConfiguration GetConfiguration()
            => config;
    }
}