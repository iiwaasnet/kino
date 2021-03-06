﻿using System.Collections.Generic;
using kino.Cluster.Configuration;

namespace kino.Cluster
{
    public interface IRendezvousCluster
    {
        RendezvousEndpoint GetCurrentRendezvousServer();

        bool SetCurrentRendezvousServer(RendezvousEndpoint newRendezvousServer);

        void RotateRendezvousServers();

        void Reconfigure(IEnumerable<RendezvousEndpoint> newConfiguration);

        IEnumerable<RendezvousEndpoint> Nodes { get; }
    }
}