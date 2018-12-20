using System.Collections.Generic;

namespace kino.Rendezvous.Configuration
{
    public interface IPartnerNetworksConfigurationProvider
    {
        void Update(PartnerNetworksConfiguration newPartnerNetworks);

        IEnumerable<PartnerClusterConfiguration> PartnerNetworks { get; }
    }
}