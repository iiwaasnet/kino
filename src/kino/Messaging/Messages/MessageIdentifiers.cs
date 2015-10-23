using kino.Connectivity;

namespace kino.Messaging.Messages
{
    public class MessageIdentifiers
    {
        public static readonly MessageIdentifier DiscoverMessageRoute = MessageIdentifier.Create<DiscoverMessageRouteMessage>();
        public static readonly MessageIdentifier RequestClusterMessageRoutes = MessageIdentifier.Create<RequestClusterMessageRoutesMessage>();
        public static readonly MessageIdentifier RequestNodeMessageRoutes = MessageIdentifier.Create<RequestNodeMessageRoutesMessage>();
        public static readonly MessageIdentifier UnregisterNodeMessageRoute = MessageIdentifier.Create<UnregisterNodeMessageRouteMessage>();
        public static readonly MessageIdentifier RegisterExternalMessageRoute = MessageIdentifier.Create<RegisterExternalMessageRouteMessage>();
        public static readonly MessageIdentifier UnregisterMessageRoute = MessageIdentifier.Create<UnregisterMessageRouteMessage>();
    }
}