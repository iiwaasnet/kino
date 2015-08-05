using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IClusterConfigurationMonitor
    {
        void Start();
        void Stop();
        void RegisterSelf(IEnumerable<MessageHandlerIdentifier> messageHandlers);
        void RequestMessageHandlersRouting()
    }
}