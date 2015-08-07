using rawf.Connectivity;

namespace Client
{
    public class ClusterConfigurationProvider : IClusterConfigurationProvider
    {
        private readonly IClusterConfiguration config;

        public ClusterConfigurationProvider(ApplicationConfiguration appConfig)
        {
            config = new ClusterConfiguration
                     {
                         PingSilenceBeforeRendezvousFailover = appConfig.PingSilenceBeforeRendezvousFailover,
                         PongSilenceBeforeRouteDeletion = appConfig.PongSilenceBeforeRouteDeletion
                     };
        }

        public IClusterConfiguration GetConfiguration()
        {
            return config;
        }
    }
}