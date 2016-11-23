using System.Collections.Generic;
using kino.Core;

namespace kino.Cluster
{
    public class NullClusterMonitor : IClusterMonitor
    {
        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void RegisterSelf(IEnumerable<Identifier> messageHandlers, string domain)
        {
        }

        public void UnregisterSelf(IEnumerable<Identifier> messageIdentifiers)
        {
        }

        public void DiscoverMessageRoute(Identifier messageIdentifier)
        {
        }
    }
}