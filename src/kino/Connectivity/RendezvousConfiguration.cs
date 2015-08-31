using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Framework;

namespace kino.Connectivity
{
    public class RendezvousConfiguration : IRendezvousConfiguration
    {
        private readonly HashedLinkedList<RendezvousEndpoints> config;
        private readonly object @lock = new object();

        public RendezvousConfiguration(IEnumerable<RendezvousEndpoints> initialConfiguration)
        {
            config = new HashedLinkedList<RendezvousEndpoints>();
            config.AddAll(initialConfiguration);

            AssertInitialConfigContainsDistinctEndpoints(config, initialConfiguration);
        }

        private void AssertInitialConfigContainsDistinctEndpoints(HashedLinkedList<RendezvousEndpoints> config, IEnumerable<RendezvousEndpoints> initialConfiguration)
        {
            if (config.Count < initialConfiguration.Count())
            {
                throw new DuplicatedKeyException("Initial Rendezvous configuration contains duplicated endpoints!");
            }
        }

        public RendezvousEndpoints GetCurrentRendezvousServer()
        {
            lock (@lock)
            {
                return config.First;
            }
        }

        public void RotateRendezvousServers()
        {
            lock (@lock)
            {
                config.InsertLast(config.RemoveFirst());
            }
        }

        public void SetCurrentRendezvousServer(RendezvousEndpoints currentRendezvousServer)
        {
            lock (@lock)
            {
                for (var i = 0; i < config.Count; i++)
                {
                    var server = config[i];
                    if (server.Equals(currentRendezvousServer))
                    {
                        config.Remove(server);
                        config.InsertFirst(server);
                    }
                }
            }
        }
    }
}