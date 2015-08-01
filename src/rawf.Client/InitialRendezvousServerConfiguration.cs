using System.Collections.Generic;
using rawf.Connectivity;
using TypedConfigProvider;

namespace rawf.Client
{
    public class InitialRendezvousServerConfiguration : IInitialRendezvousServerConfiguration
    {
        private readonly IEnumerable<RendezvousServerConfiguration> config;
        public InitialRendezvousServerConfiguration(IConfigProvider configProvider)
        {
        }

        public IEnumerable<RendezvousServerConfiguration> GetConfiguration()
        {
            return config;
        }
    }
}