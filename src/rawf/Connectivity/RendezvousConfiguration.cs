using System.Collections.Generic;

namespace rawf.Connectivity
{
    public class RendezvousConfiguration : IRendezvousConfiguration
    {
        private readonly IEnumerable<RendezvousServerConfiguration> initialConfiguration;

        public RendezvousConfiguration(IEnumerable<RendezvousServerConfiguration> initialConfiguration)
        {
            this.initialConfiguration = initialConfiguration;
        }

        public IEnumerable<RendezvousServerConfiguration> GetRendezvousServers()
            => initialConfiguration;
    }
}