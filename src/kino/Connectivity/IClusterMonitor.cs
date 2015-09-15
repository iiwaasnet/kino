using System.Collections.Generic;

namespace kino.Connectivity
{
    public interface IClusterMonitor
    {
        void Start();
        void Stop();
        void RegisterSelf(IEnumerable<MessageHandlerIdentifier> messageHandlers);
        void RequestMessageHandlersRouting();
        void UnregisterSelf(IEnumerable<MessageHandlerIdentifier> messageHandlers);
    }
}