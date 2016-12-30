using System.Collections.Generic;

namespace kino.Cluster
{
    public interface IClusterMonitor
    {
        void Start();

        void Stop();

        void RegisterSelf(IEnumerable<MessageRoute> registrations, string domain);

        void UnregisterSelf(IEnumerable<MessageRoute> messageRoutes);

        void DiscoverMessageRoute(MessageRoute messageRoute);
    }
}