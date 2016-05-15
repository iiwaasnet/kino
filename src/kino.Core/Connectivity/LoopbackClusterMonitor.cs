using System;
using System.Collections.Generic;
using System.Linq;

namespace kino.Core.Connectivity
{
    internal class LoopbackClusterMonitor : IClusterMonitor
    {
        public bool Start(TimeSpan startTimeout)
            => true;

        public void Stop()
        {
        }

        public void RegisterSelf(IEnumerable<MessageIdentifier> messageHandlers)
        {
        }

        public void RequestClusterRoutes()
        {
        }

        public void UnregisterSelf(IEnumerable<MessageIdentifier> messageIdentifiers)
        {
        }

        public IEnumerable<SocketEndpoint> GetClusterMembers()
            => Enumerable.Empty<SocketEndpoint>();

        public void DiscoverMessageRoute(MessageIdentifier messageIdentifier)
        {
        }
    }
}