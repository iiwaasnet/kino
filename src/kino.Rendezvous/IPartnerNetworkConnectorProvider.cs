using System.Collections.Generic;

namespace kino.Rendezvous
{
    public interface IPartnerNetworkConnectorProvider
    {
        IEnumerable<PartnerNetworkConnector> GetPartnerConnectors();
    }
}