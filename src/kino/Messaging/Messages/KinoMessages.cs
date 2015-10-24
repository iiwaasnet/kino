using kino.Connectivity;

namespace kino.Messaging.Messages
{
    public class KinoMessages
    {
        public static readonly MessageIdentifier DiscoverMessageRoute = MessageIdentifier.Create<DiscoverMessageRouteMessage>();
        public static readonly MessageIdentifier RequestClusterMessageRoutes = MessageIdentifier.Create<RequestClusterMessageRoutesMessage>();
        public static readonly MessageIdentifier RequestNodeMessageRoutes = MessageIdentifier.Create<RequestNodeMessageRoutesMessage>();
        public static readonly MessageIdentifier UnregisterNodeMessageRoute = MessageIdentifier.Create<UnregisterNodeMessageRouteMessage>();
        public static readonly MessageIdentifier RegisterExternalMessageRoute = MessageIdentifier.Create<RegisterExternalMessageRouteMessage>();
        public static readonly MessageIdentifier UnregisterMessageRoute = MessageIdentifier.Create<UnregisterMessageRouteMessage>();
        public static readonly MessageIdentifier Ping = MessageIdentifier.Create<PingMessage>();
        public static readonly MessageIdentifier Pong = MessageIdentifier.Create<PongMessage>();
        public static readonly MessageIdentifier RendezvousNotLeader = MessageIdentifier.Create<RendezvousNotLeaderMessage>();
        public static readonly MessageIdentifier RendezvousConfigurationChanged = MessageIdentifier.Create<RendezvousConfigurationChangedMessage>();
        public static readonly MessageIdentifier Exception = MessageIdentifier.Create<ExceptionMessage>();
    }
}