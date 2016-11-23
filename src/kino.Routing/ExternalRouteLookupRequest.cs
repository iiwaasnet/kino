using kino.Core;

namespace kino.Routing
{
    public class ExternalRouteLookupRequest : InternalRouteLookupRequest
    {
        public ReceiverIdentifier ReceiverNodeIdentity { get; set; }
    }
}