using System.Collections.Generic;
using kino.Core.Connectivity;

namespace kino.Configuration
{
    public class ScaleOutSocketConfiguration
    {
        public IEnumerable<SocketEndpoint> AddressRange { get; set; }
    }
}