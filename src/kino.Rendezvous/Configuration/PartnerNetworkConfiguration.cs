using System.Collections.Generic;

namespace kino.Rendezvous.Configuration
{
    public class PartnerNetworkConfiguration
    {
        public IEnumerable<string> AllowedDomains { get; set; }

        public IEnumerable<string> Cluster { get; set; }
    }
}