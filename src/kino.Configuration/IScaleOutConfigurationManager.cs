using System.Collections.Generic;
using kino.Core;

namespace kino.Configuration
{
    public interface IScaleOutConfigurationManager : IScaleOutConfigurationProvider
    {
        IEnumerable<SocketEndpoint> GetScaleOutAddressRange();

        void SetActiveScaleOutAddress(SocketEndpoint activeAddress);
    }
}