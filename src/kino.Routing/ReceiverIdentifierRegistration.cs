using kino.Core;

namespace kino.Routing
{
    public class ReceiverIdentifierRegistration : ReceiverIdentifier
    {
        public ReceiverIdentifierRegistration(ReceiverIdentifier receiverIdentifier, bool localRegistration)
            : base(receiverIdentifier.Identity)
        {
            LocalRegistration = localRegistration;
        }

        public bool LocalRegistration { get; }
    }
}