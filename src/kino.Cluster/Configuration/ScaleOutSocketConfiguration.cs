using System.Collections.Generic;
using kino.Core;

namespace kino.Cluster.Configuration
{
    public class ScaleOutSocketConfiguration
    {
        public int ScaleOutReceiveMessageQueueLength { get; set; }

        public IEnumerable<SocketEndpoint> AddressRange { get; set; }
    }
}