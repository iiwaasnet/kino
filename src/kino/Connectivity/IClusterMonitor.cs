using System.Collections.Generic;

namespace kino.Connectivity
{
    public interface IClusterMonitor
    {
        void Start();
        void Stop();
        void RegisterSelf(IEnumerable<MessageIdentifier> messageHandlers);
        void RequestClusterRoutes();
        void UnregisterSelf(IEnumerable<MessageIdentifier> messageHandlers);
    }
}