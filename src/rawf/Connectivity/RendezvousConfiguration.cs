using System.Collections.Generic;

namespace rawf.Connectivity
{
    public class RendezvousConfiguration : IRendezvousConfiguration
    {
        private readonly IList<RendezvousServerConfiguration> config;
        private int currentServerIndex;

        public RendezvousConfiguration(IEnumerable<RendezvousServerConfiguration> initialConfiguration)
        {
            currentServerIndex = 0;
            config = new List<RendezvousServerConfiguration>(initialConfiguration);
        }

        public RendezvousServerConfiguration GetCurrentRendezvousServer()
            => config[currentServerIndex];

        public RendezvousServerConfiguration RotateRendezvousServers()
            => config[RotateServerIndex()];

        private int RotateServerIndex()
        {
            currentServerIndex = (config.Count <= currentServerIndex + 1)
                                     ? 0
                                     : currentServerIndex + 1;
            return currentServerIndex;
        }
    }
}