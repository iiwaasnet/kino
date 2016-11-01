using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core;

namespace kino.Cluster
{
    internal class LoopbackClusterMonitor : IClusterMonitor
    {
        public bool Start(TimeSpan startTimeout)
            => true;

        public void Stop()
        {
        }

        public void RegisterSelf(IEnumerable<Identifier> messageHandlers, string domain)
        {
        }

        public void RequestClusterRoutes()
        {
        }

        public void UnregisterSelf(IEnumerable<Identifier> messageIdentifiers)
        {
        }

        public IEnumerable<SocketEndpoint> GetClusterMembers()
            => Enumerable.Empty<SocketEndpoint>();

        public void DiscoverMessageRoute(Identifier messageIdentifier)
        {
        }
    }
}