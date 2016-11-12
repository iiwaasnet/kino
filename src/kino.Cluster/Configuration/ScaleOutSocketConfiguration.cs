using System.Collections.Generic;
using kino.Core;

namespace kino.Cluster.Configuration
{
    public class ScaleOutSocketConfiguration
    {
        public IEnumerable<SocketEndpoint> AddressRange { get; set; }
    }
}