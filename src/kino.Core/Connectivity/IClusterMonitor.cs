using System.Collections.Generic;

namespace kino.Core.Connectivity
{
    public interface IClusterMonitor
    {
        void Start();
        void Stop();
        void RegisterSelf(IEnumerable<MessageIdentifier> messageHandlers);
        void RequestClusterRoutes();
        void UnregisterSelf(IEnumerable<MessageIdentifier> messageIdentifiers);
        IEnumerable<SocketEndpoint> GetClusterMembers();
        void DiscoverMessageRoute(MessageIdentifier messageIdentifier);
    }
}