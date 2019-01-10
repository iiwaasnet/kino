using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Core.Framework;
using kino.Rendezvous.Configuration;

namespace kino.Rendezvous
{
    public class PartnerRendezvousCluster : IPartnerRendezvousCluster
    {
        private volatile HashedLinkedList<PartnerRendezvousEndpoint> config;
        private readonly object @lock = new object();

        public PartnerRendezvousCluster(IEnumerable<PartnerRendezvousEndpoint> initialConfiguration)
        {
            config = new HashedLinkedList<PartnerRendezvousEndpoint>();

            config.AddAll(SelectEndpointsDistinct(initialConfiguration));
        }

        private static IEnumerable<PartnerRendezvousEndpoint> SelectEndpointsDistinct(IEnumerable<PartnerRendezvousEndpoint> initialConfiguration)
        {
            var tmp = new HashedLinkedList<PartnerRendezvousEndpoint>();
            tmp.AddAll(initialConfiguration);

            if (tmp.Count < initialConfiguration.Count())
            {
                throw new DuplicatedKeyException("Initial Rendezvous configuration contains duplicated endpoints!");
            }

            return tmp;
        }

        public PartnerRendezvousEndpoint GetCurrentRendezvousServer()
        {
            lock (@lock)
            {
                return config.First;
            }
        }

        public bool SetCurrentRendezvousServer(PartnerRendezvousEndpoint newRendezvousServer)
        {
            lock (@lock)
            {
                for (var i = 0; i < config.Count; i++)
                {
                    var server = config[i];
                    server.RefreshUri();

                    if (server.Equals(newRendezvousServer))
                    {
                        config.Remove(server);
                        config.InsertFirst(server);

                        return true;
                    }
                }

                return false;
            }
        }

        public void RotateRendezvousServers()
        {
            lock (@lock)
            {
                var oldCurrent = config.RemoveFirst();
                config.InsertLast(oldCurrent);
                oldCurrent.RefreshUri();
            }
        }

        public void Reconfigure(IEnumerable<PartnerRendezvousEndpoint> newConfiguration)
        {
            var tmp = new HashedLinkedList<PartnerRendezvousEndpoint>();
            tmp.AddAll(SelectEndpointsDistinct(newConfiguration));

            lock (@lock)
            {
                config = tmp;
            }
        }

        public IEnumerable<PartnerRendezvousEndpoint> Nodes
        {
            get
            {
                lock (@lock)
                {
                    return config.ToList();
                }
            }
        }
    }
}