using System.Collections.Generic;

namespace kino.Rendezvous.Configuration
{
    public interface IPartnerNetworksConfigurationProvider
    {
        void Update(IEnumerable<PartnerNetworkConfiguration> partners);

        IEnumerable<PartnerClusterConfiguration> PartnerNetworks { get; }
    }
}