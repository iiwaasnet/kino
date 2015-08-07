using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IRendezvousConfiguration
    {
        RendezvousServerConfiguration GetCurrentRendezvousServers();
        RendezvousServerConfiguration GetNextRendezvousServers();
    }
}