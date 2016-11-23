using kino.Core;
using kino.Messaging;

namespace kino.Routing
{
    public class InternalRouteLookupRequest
    {
        public ReceiverIdentifier ReceiverIdentity { get; set; }

        public MessageIdentifier Message { get; set; }

        public DistributionPattern Distribution { get; set; }
    }
}