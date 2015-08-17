using System.Collections.Generic;
using System.Linq;
using C5;
using rawf.Framework;

namespace rawf.Connectivity
{
    public class RendezvousConfiguration : IRendezvousConfiguration
    {
        private readonly HashedLinkedList<RendezvousServerConfiguration> config;

        public RendezvousConfiguration(IEnumerable<RendezvousServerConfiguration> initialConfiguration)
        {
            config = new HashedLinkedList<RendezvousServerConfiguration>(new ConfigurationEqualityComparer());
            config.AddAll(initialConfiguration);

            AssertInitialConfigContainsDistinctEndpoints(config, initialConfiguration);
        }

        private void AssertInitialConfigContainsDistinctEndpoints(HashedLinkedList<RendezvousServerConfiguration> config, IEnumerable<RendezvousServerConfiguration> initialConfiguration)
        {
            if (config.Count < initialConfiguration.Count())
            {
                throw new DuplicatedKeyException("Initial Rendezvous configuration contains duplicated endpoints!");
            }
        }

        public RendezvousServerConfiguration GetCurrentRendezvousServer()
            => config.First;

        public void RotateRendezvousServers()
            => config.InsertLast(config.RemoveFirst());

        public void SetCurrentRendezvousServer(RendezvousServerConfiguration currentRendezvousServer)
        {
            for (var i = 0; i < config.Count; i++)
            {
                var server = config[i];
                if (server.BroadcastUri.Equals(currentRendezvousServer.BroadcastUri)
                    && server.UnicastUri.Equals(currentRendezvousServer.UnicastUri))
                {
                    config.Remove(server);
                    config.InsertFirst(server);
                }
            }
        }

        private class ConfigurationEqualityComparer : IEqualityComparer<RendezvousServerConfiguration>
        {
            public bool Equals(RendezvousServerConfiguration x, RendezvousServerConfiguration y)
            {
                return x.BroadcastUri.Equals(y.BroadcastUri) && x.UnicastUri.Equals(y.UnicastUri);
            }

            public int GetHashCode(RendezvousServerConfiguration obj)
            {
                unchecked
                {
                    return obj.BroadcastUri.GetHashCode() ^ obj.UnicastUri.GetHashCode();
                }
            }
        }
    }
}