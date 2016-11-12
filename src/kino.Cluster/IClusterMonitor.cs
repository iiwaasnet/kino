using System.Collections.Generic;
using kino.Core;

namespace kino.Cluster
{
    public interface IClusterMonitor
    {
        void Start();

        void Stop();

        void RegisterSelf(IEnumerable<Identifier> messageHandlers, string domain);

        void UnregisterSelf(IEnumerable<Identifier> messageIdentifiers);

        void DiscoverMessageRoute(Identifier messageIdentifier);
    }
}