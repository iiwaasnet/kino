using System.Collections.Generic;
using kino.Messaging;

namespace kino.Connectivity
{
    public interface IClusterMonitor
    {
        void Start();
        void Stop();
        void RegisterSelf(IEnumerable<IMessageIdentifier> messageHandlers);
        void RequestClusterRoutes();
        void UnregisterSelf(IEnumerable<IMessageIdentifier> messageIdentifiers);
        IEnumerable<SocketEndpoint> GetClusterMembers();
        void DiscoverMessageRoute(IMessageIdentifier messageIdentifier);
    }
}