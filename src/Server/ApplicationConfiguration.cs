using System.Collections.Generic;
using rawf.Actors;

namespace Server
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