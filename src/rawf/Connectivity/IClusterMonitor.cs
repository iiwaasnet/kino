using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IClusterMonitor
    {
        void Start();
        void Stop();
        void RegisterSelf(IEnumerable<MessageHandlerIdentifier> messageHandlers);
        void RequestMessageHandlersRouting();
    }
}