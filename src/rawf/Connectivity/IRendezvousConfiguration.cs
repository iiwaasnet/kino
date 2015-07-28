using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IRendezvousConfiguration
    {
        IEnumerable<RendezvousServerConfiguration> GetAdsServers();
    }
}