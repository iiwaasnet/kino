using System.Collections.Generic;
using kino.Core;

namespace kino.Cluster
{
    public interface IClusterMonitor
    {
        void Start();

        void Stop();

        void RegisterSelf(IEnumerable<MessageRoute> registrations, string domain);

        void UnregisterSelf(IEnumerable<MessageRoute> registrations);

        void DiscoverMessageRoute(Identifier messageIdentifier);
    }
}