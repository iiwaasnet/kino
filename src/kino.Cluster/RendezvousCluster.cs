﻿using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Cluster.Configuration;
using kino.Core.Framework;

namespace kino.Cluster
{
    //TODO: review locking
    public class RendezvousCluster : IRendezvousCluster
    {
        private volatile HashedLinkedList<RendezvousEndpoint> config;
        private readonly object @lock = new object();

        public RendezvousCluster(IEnumerable<RendezvousEndpoint> initialConfiguration)
        {
            config = new HashedLinkedList<RendezvousEndpoint>();

            config.AddAll(SelectEndpointsDistinct(initialConfiguration));
        }

        public void Reconfigure(IEnumerable<RendezvousEndpoint> newConfiguration)
        {
            var tmp = new HashedLinkedList<RendezvousEndpoint>();
            tmp.AddAll(SelectEndpointsDistinct(newConfiguration));

            lock (@lock)
            {
                config = tmp;
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