using System.Collections.Generic;
using rawf.Client;

namespace Client
{
    public class ApplicationConfiguration
    {
        public string RouterUri { get; set; }
        public byte[] RouterSocketIdentity { get; set; }
        public string ScaleOutAddressUri { get; set; }
        public byte[] ScaleOutSocketIdentity { get; set; }
        public IEnumerable<RendezvousEndpoint> RendezvousServers { get; set; }
    }
}