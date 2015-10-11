using System.Collections.Generic;
using System.Linq;

namespace kino.Connectivity
{
    internal class LoopbackClusterMonitor : IClusterMonitor
    {
        public void Start()
        {
        }

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