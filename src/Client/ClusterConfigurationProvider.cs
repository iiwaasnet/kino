using rawf.Connectivity;
using rawf.Diagnostics;

namespace Client
{
    public class ClusterConfigurationProvider : IClusterConfigurationProvider
    {
        private readonly IClusterConfiguration config;

        public ClusterConfigurationProvider(ApplicationConfiguration appConfig, ILogger logger)
        {
            config = new ClusterConfiguration(logger)
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