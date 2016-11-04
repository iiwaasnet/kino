using kino.Core;

namespace kino.Routing.ServiceMessageHandlers
{
    public static class IdentifierExtensions
    {
        public static Identifier ToIdentifier(this Messaging.Messages.MessageContract contract)
            => contract.IsAnyIdentifier
                   ? (Identifier) new AnyIdentifier(contract.Identity)
                   : (Identifier) new MessageIdentifier(contract.Identity, contract.Version, contract.Partition);
    }
}