using System;
using System.Collections.Generic;
using System.Linq;
using rawf.Connectivity;
using TypedConfigProvider;

namespace Server
{
    public class InitialRendezvousServerConfiguration : IInitialRendezvousServerConfiguration
    {
        private readonly IEnumerable<RendezvousServerConfiguration> config;
        public InitialRendezvousServerConfiguration(IConfigProvider configProvider)
        {
            var tmp = configProvider.GetConfiguration<ApplicationConfiguration>();

            config = tmp.RendezvousServers.Select(rs => new RendezvousServerConfiguration
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