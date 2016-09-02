using System.Collections.Generic;

namespace kino.Core.Connectivity
{
    public class ScaleOutSocketConfiguration
    {
        public IEnumerable<SocketEndpoint> AddressRange { get; set; }
    }
}