using System.Collections.Generic;

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

        public void RegisterSelf(IEnumerable<MessageRoute> registrations, string domain)
        {
        }

        public void UnregisterSelf(IEnumerable<MessageRoute> messageRoutes)
        {
        }

        public void DiscoverMessageRoute(MessageRoute messageRoute)
        {
        }
    }
}