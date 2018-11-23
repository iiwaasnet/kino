using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Cluster.Configuration;
using kino.Core.Diagnostics;

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
            configurationStorage.Update(new RendezvousClusterConfiguration {Cluster = newConfiguration});
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
                var oldCurrent = config.RemoveFirst();
                config.InsertLast(oldCurrent);
                oldCurrent.RefreshUri();
            }
        }

        public bool SetCurrentRendezvousServer(RendezvousEndpoint newRendezvousServer)
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

        public IEnumerable<RendezvousEndpoint> Nodes
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