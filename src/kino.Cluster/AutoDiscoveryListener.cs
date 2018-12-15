using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Cluster
{
    public class AutoDiscoveryListener : AutoDiscoveryBaseListener
    {
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;

        public AutoDiscoveryListener(IRendezvousCluster rendezvousCluster,
                                     ISocketFactory socketFactory,
                                     ILocalSocketFactory localSocketFactory,
                                     IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                                     ClusterMembershipConfiguration membershipConfiguration,
                                     IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                     ILogger logger)
            : base(rendezvousCluster,
                   socketFactory,
                   membershipConfiguration.HeartBeatSilenceBeforeRendezvousFailover,
                   performanceCounterManager,
                   localSocketFactory.CreateNamed<IMessage>(NamedSockets.RouterLocalSocket),
                   logger)
            => this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;

        protected override bool IsDiscoverMessageRouteMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.DiscoverMessageRoute))
            {
                var payload = message.GetPayload<DiscoverMessageRouteMessage>();

                return !ThisNodeSocket(payload.RequestorNodeIdentity);
            }

            return false;
        }

        protected override bool IsRequestClusterMessageRoutesMessage(IMessage message)
        {
            if (base.IsRequestClusterMessageRoutesMessage(message))
            {
                var payload = message.GetPayload<RequestClusterMessageRoutesMessage>();

                return !ThisNodeSocket(payload.RequestorNodeIdentity);
            }

            return false;
        }

        protected override bool IsRequestNodeMessageRoutingMessage(IMessage message)
        {
            if (base.IsRequestNodeMessageRoutingMessage(message))
            {
                var payload = message.GetPayload<RequestNodeMessageRoutesMessage>();

                return ThisNodeSocket(payload.TargetNodeIdentity);
            }

            return false;
        }

        protected override bool IsUnregisterNodeMessage(IMessage message)
        {
            if (base.IsUnregisterNodeMessage(message))
            {
                var payload = message.GetPayload<UnregisterNodeMessage>();

                return !ThisNodeSocket(payload.ReceiverNodeIdentity);
            }

            return message.Equals(KinoMessages.UnregisterUnreachableNode);
        }

        protected override bool IsRegisterExternalRoute(IMessage message)
        {
            if (base.IsRegisterExternalRoute(message))
            {
                var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();

                return !ThisNodeSocket(payload.NodeIdentity);
            }

            return false;
        }

        protected override bool IsUnregisterMessageRoutingMessage(IMessage message)
        {
            if (base.IsUnregisterMessageRoutingMessage(message))
            {
                var payload = message.GetPayload<UnregisterMessageRouteMessage>();

                return !ThisNodeSocket(payload.ReceiverNodeIdentity);
            }

            return false;
        }

        private bool ThisNodeSocket(byte[] socketIdentity)
        {
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
            return Unsafe.ArraysEqual(scaleOutAddress.Identity, socketIdentity);
        }
    }
}