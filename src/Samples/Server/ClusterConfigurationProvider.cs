using kino.Connectivity;
using kino.Diagnostics;

namespace Server
{
    public class ClusterConfigurationProvider : IClusterConfigurationProvider
    {
        private readonly IClusterConfiguration config;

        public ClusterConfigurationProvider(ApplicationConfiguration appConfig, ILogger logger)
        {
            config = new ClusterConfiguration(new ClusterTimingConfiguration
                                              {
                                                  PingSilenceBeforeRendezvousFailover = appConfig.PingSilenceBeforeRendezvousFailover,
                                                  PongSilenceBeforeRouteDeletion = appConfig.PongSilenceBeforeRouteDeletion,
                                                  ExpectedPingInterval = appConfig.ExpectedPingInterval
                                              },
                                              logger);
        }

        public IClusterConfiguration GetConfiguration()
        {
            return config;
        }
    }
}