using kino.Core;

namespace kino.Routing
{
    public class MessageHubRoute
    {
        public ReceiverIdentifier MessageHub { get; set; }

        public bool LocalRegistration { get; set; }
    }
}