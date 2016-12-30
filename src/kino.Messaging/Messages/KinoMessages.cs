using kino.Core;

namespace kino.Messaging.Messages
{
    public class KinoMessages
    {
        public static readonly MessageIdentifier DiscoverMessageRoute = MessageIdentifier.Create<DiscoverMessageRouteMessage>();
        public static readonly MessageIdentifier RequestClusterMessageRoutes = MessageIdentifier.Create<RequestClusterMessageRoutesMessage>();
        public static readonly MessageIdentifier RequestNodeMessageRoutes = MessageIdentifier.Create<RequestNodeMessageRoutesMessage>();
        public static readonly MessageIdentifier UnregisterNode = MessageIdentifier.Create<UnregisterNodeMessage>();
        public static readonly MessageIdentifier UnregisterUnreachableNode = MessageIdentifier.Create<UnregisterUnreachableNodeMessage>();
        public static readonly MessageIdentifier RegisterExternalMessageRoute = MessageIdentifier.Create<RegisterExternalMessageRouteMessage>();
        public static readonly MessageIdentifier UnregisterMessageRoute = MessageIdentifier.Create<UnregisterMessageRouteMessage>();
        public static readonly MessageIdentifier Ping = MessageIdentifier.Create<PingMessage>();
        public static readonly MessageIdentifier Pong = MessageIdentifier.Create<PongMessage>();
        public static readonly MessageIdentifier RendezvousNotLeader = MessageIdentifier.Create<RendezvousNotLeaderMessage>();
        public static readonly MessageIdentifier RendezvousConfigurationChanged = MessageIdentifier.Create<RendezvousConfigurationChangedMessage>();
        public static readonly MessageIdentifier Exception = MessageIdentifier.Create<ExceptionMessage>();
        public static readonly MessageIdentifier RequestKnownMessageRoutes = MessageIdentifier.Create<RequestKnownMessageRoutesMessage>();
        public static readonly MessageIdentifier HeartBeat = MessageIdentifier.Create<HeartBeatMessage>();
        public static readonly MessageIdentifier StartPeerMonitoring = MessageIdentifier.Create<StartPeerMonitoringMessage>();
        public static readonly MessageIdentifier CheckDeadPeers = MessageIdentifier.Create<CheckDeadPeersMessage>();
        public static readonly MessageIdentifier CheckStalePeers = MessageIdentifier.Create<CheckStalePeersMessage>();
        public static readonly MessageIdentifier DeletePeer = MessageIdentifier.Create<DeletePeerMessage>();
        public static readonly MessageIdentifier AddPeer = MessageIdentifier.Create<AddPeerMessage>();
        public static readonly MessageIdentifier RequestMessageExternalRoutes = MessageIdentifier.Create<RequestMessageExternalRoutesMessage>();
        public static readonly MessageIdentifier MessageExternalRoutes = MessageIdentifier.Create<MessageExternalRoutesMessage>();
        public static readonly MessageIdentifier CheckPeerConnection = MessageIdentifier.Create<CheckPeerConnectionMessage>();
    }
}