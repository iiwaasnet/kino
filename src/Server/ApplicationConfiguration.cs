using System.Collections.Generic;
using rawf.Actors;

namespace Server
{
    public class ApplicationConfiguration
    {
        public string RouterUri { get; set; }
        public string ScaleOutAddressUri { get; set; }
        public IEnumerable<RendezvousEndpoint> RendezvousServers { get; set; }
    }
}