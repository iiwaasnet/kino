using System;
using System.Collections.Generic;
using System.Linq;
using kino.Connectivity;

namespace Server
{
    public class RendezvousEndpointsProvider : IRendezvousEndpointsProvider
    {
        private readonly IEnumerable<RendezvousEndpoints> config;

        public RendezvousEndpointsProvider(ApplicationConfiguration appConfig)
        {
            config = appConfig.RendezvousServers.Select(rs => new RendezvousEndpoints
                                                              {
                                                                  MulticastUri = new Uri(rs.BroadcastUri),
                                                                  UnicastUri = new Uri(rs.UnicastUri)
                                                              });
        }

        public IEnumerable<RendezvousEndpoints> GetConfiguration()
        {
            return config;
        }
    }
}