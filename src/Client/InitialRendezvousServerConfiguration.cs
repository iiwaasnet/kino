using System;
using System.Collections.Generic;
using System.Linq;
using rawf.Connectivity;
using TypedConfigProvider;

namespace Client
{
    public class InitialRendezvousServerConfiguration : IInitialRendezvousServerConfiguration
    {
        private readonly IEnumerable<RendezvousServerConfiguration> config;
        public InitialRendezvousServerConfiguration(ApplicationConfiguration appConfig)
        {
            config = appConfig.RendezvousServers.Select(rs => new RendezvousServerConfiguration
                                                        {
                                                            BroadcastUri = new Uri(rs.BroadcastUri),
                                                            UnicastUri = new Uri(rs.UnicastUri)
                                                        });
        }

        public IEnumerable<RendezvousServerConfiguration> GetConfiguration()
        {
            return config;
        }
    }
}