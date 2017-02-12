using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using kino.Core;

namespace kino.Cluster.Configuration
{
    [ExcludeFromCodeCoverage]
    public class ScaleOutSocketConfiguration
    {
        public int ScaleOutReceiveMessageQueueLength { get; set; }

        public IEnumerable<SocketEndpoint> AddressRange { get; set; }
    }
}