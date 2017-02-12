using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Core.Framework;

namespace kino.Cluster.Configuration
{
    public class RendezvousClusterConfigurationReadonlyStorage : IConfigurationStorage<RendezvousClusterConfiguration>
    {
        private volatile HashedLinkedList<RendezvousEndpoint> config;

        public RendezvousClusterConfigurationReadonlyStorage(IEnumerable<RendezvousEndpoint> initialConfiguration)
        {
            config = new HashedLinkedList<RendezvousEndpoint>();

            config.AddAll(SelectEndpointsDistinct(initialConfiguration));
        }

        public RendezvousClusterConfiguration Read()
            => new RendezvousClusterConfiguration {Cluster = config};

        public void Update(RendezvousClusterConfiguration newConfig)
        {
            var tmp = new HashedLinkedList<RendezvousEndpoint>();
            tmp.AddAll(SelectEndpointsDistinct(newConfig.Cluster));

            config = tmp;
        }

        private static IEnumerable<RendezvousEndpoint> SelectEndpointsDistinct(IEnumerable<RendezvousEndpoint> initialConfiguration)
        {
            var tmp = new HashedLinkedList<RendezvousEndpoint>();
            tmp.AddAll(initialConfiguration);

            if (tmp.Count < initialConfiguration.Count())
            {
                throw new DuplicatedKeyException("Initial Rendezvous configuration contains duplicated endpoints!");
            }

            return tmp;
        }
    }
}