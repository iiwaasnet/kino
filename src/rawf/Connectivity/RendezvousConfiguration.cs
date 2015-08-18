using System.Collections.Generic;
using System.Linq;
using C5;
using rawf.Framework;

namespace rawf.Connectivity
{
    public class RendezvousConfiguration : IRendezvousConfiguration
    {
        private readonly HashedLinkedList<RendezvousServerConfiguration> config;
        private readonly object @lock = new object();

        public RendezvousConfiguration(IEnumerable<RendezvousServerConfiguration> initialConfiguration)
        {
            config = new HashedLinkedList<RendezvousServerConfiguration>();
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

        public void SetCurrentRendezvousServer(RendezvousServerConfiguration currentRendezvousServer)
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

        //private class ConfigurationEqualityComparer : IEqualityComparer<RendezvousServerConfiguration>
        //{
        //    public bool Equals(RendezvousServerConfiguration x, RendezvousServerConfiguration y)
        //    {
        //        return x.MulticastUri.Equals(y.MulticastUri) && x.UnicastUri.Equals(y.UnicastUri);
        //    }

        //    public int GetHashCode(RendezvousServerConfiguration obj)
        //    {
        //        unchecked
        //        {
        //            return obj.MulticastUri.GetHashCode() ^ obj.UnicastUri.GetHashCode();
        //        }
        //    }
        //}
    }
}