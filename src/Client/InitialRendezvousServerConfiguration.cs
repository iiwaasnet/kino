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
        public InitialRendezvousServerConfiguration(IConfigProvider configProvider)
        {
            var tmp = configProvider.GetConfiguration<ApplicationConfiguration>();

            config = tmp.RendezvousServers.Select(rs => new RendezvousServerConfiguration
                                                        {
                                                            BroadcastEndpoint = new Uri(rs.BroadcastUri),
                                                            UnicastEndpoint = new SocketEndpoint(new Uri(rs.UnicastUri), rs.UnicastSocketIdentity)
                                                        });
        }

        public IEnumerable<RendezvousServerConfiguration> GetConfiguration()
        {
            return config;
        }
    }
}