using System.Collections.Generic;
using kino.Core.Connectivity;

namespace kino.Configuration
{
    public interface IRouterConfigurationManager : IRouterConfigurationProvider
    {
        IEnumerable<SocketEndpoint> GetScaleOutAddressRange();

        void SetActiveScaleOutAddress(SocketEndpoint activeAddress);
    }
}