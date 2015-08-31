using kino.Connectivity;
using kino.Diagnostics;

namespace Server
{
    public class ClusterConfigurationProvider : IClusterConfigurationProvider
    {
        private readonly IClusterConfiguration config;

        public ClusterConfigurationProvider(ApplicationConfiguration appConfig, ILogger logger)
        {
            config = new ClusterConfiguration(logger)
                     {
                         PingSilenceBeforeRendezvousFailover = appConfig.PingSilenceBeforeRendezvousFailover,
                         PongSilenceBeforeRouteDeletion = appConfig.PongSilenceBeforeRouteDeletion,
                     };
        }

        public IClusterConfiguration GetConfiguration()
        {
            return config;
        }
    }
}