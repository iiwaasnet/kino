using System.Collections.Generic;

namespace kino.Core.Connectivity
{
    public interface IRouterConfigurationManager : IRouterConfigurationProvider
    {
        IEnumerable<SocketEndpoint> GetScaleOutAddressRange();

        void SetActiveScaleOutAddress(SocketEndpoint activeAddress);
    }
}