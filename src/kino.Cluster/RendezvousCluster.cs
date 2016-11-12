using System.Collections.Generic;
using C5;
using kino.Cluster.Configuration;

namespace kino.Cluster
{
    public class RendezvousCluster : IRendezvousCluster
    {
        private readonly HashedLinkedList<RendezvousEndpoint> config;
        private readonly IConfigurationStorage<RendezvousClusterConfiguration> configurationStorage;
        private readonly object @lock = new object();

        public RendezvousCluster(IConfigurationStorage<RendezvousClusterConfiguration> configurationStorage)
        {
            this.configurationStorage = configurationStorage;

            config = new HashedLinkedList<RendezvousEndpoint>();
            config.AddAll(configurationStorage.Read().Cluster);
        }

        public void Reconfigure(IEnumerable<RendezvousEndpoint> newConfiguration)
        {
            configurationStorage.Update(new RendezvousClusterConfiguration { Cluster = newConfiguration });
            lock (@lock)
            {
                config.Clear();
                config.AddAll(configurationStorage.Read().Cluster);
            }
        }

        public RendezvousEndpoint GetCurrentRendezvousServer()
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

        public void SetCurrentRendezvousServer(RendezvousEndpoint currentRendezvousServer)
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