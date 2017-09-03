using System;
using System.Collections.Generic;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Cluster
{
    public class RouteDiscovery : IRouteDiscovery
    {
        private readonly IAutoDiscoverySender autoDiscoverySender;
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly ISecurityProvider securityProvider;
        private readonly RouteDiscoveryConfiguration discoveryConfiguration;
        private readonly ILogger logger;
        private readonly HashedQueue<MessageRoute> requests;
        private Task sendingMessages;
        private CancellationTokenSource cancellationTokenSource;

        public RouteDiscovery(IAutoDiscoverySender autoDiscoverySender,
                              IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                              ClusterMembershipConfiguration clusterMembershipConfiguration,
                              ISecurityProvider securityProvider,
                              ILogger logger)
        {
            this.securityProvider = securityProvider;
            discoveryConfiguration = clusterMembershipConfiguration.RouteDiscovery;
            this.autoDiscoverySender = autoDiscoverySender;
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.logger = logger;
            requests = new HashedQueue<MessageRoute>(discoveryConfiguration.MaxMissingRouteDiscoveryRequestQueueLength);
        }

        public void Start()
        {
            cancellationTokenSource = new CancellationTokenSource();
            sendingMessages = Task.Factory.StartNew(_ => ThrottleRouteDiscoveryRequests(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            sendingMessages?.Wait();
        }

        private void ThrottleRouteDiscoveryRequests(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    IList<MessageRoute> missingRoutes;
                    if (requests.TryPeek(out missingRoutes, discoveryConfiguration.MissingRoutesDiscoveryRequestsPerSend))
                    {
                        foreach (var messageRoute in missingRoutes)
                        {
                            SendDiscoverMessageRouteMessage(messageRoute);
                        }
                    }

                    discoveryConfiguration.MissingRoutesDiscoverySendingPeriod.Sleep(token);

                    requests.TryDelete(missingRoutes);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception err)
                {
                    logger.Error(err);
                }
            }
        }

        private void SendDiscoverMessageRouteMessage(MessageRoute messageRoute)
        {
            try
            {
                var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
                var domains = messageRoute.Receiver.IsMessageHub()
                                  ? securityProvider.GetAllowedDomains()
                                  : new[] {securityProvider.GetDomain(messageRoute.Message.Identity)};
                foreach (var domain in domains)
                {
                    var message = Message.Create(new DiscoverMessageRouteMessage
                                                 {
                                                     RequestorNodeIdentity = scaleOutAddress.Identity,
                                                     RequestorUri = scaleOutAddress.Uri.ToSocketAddress(),
                                                     ReceiverIdentity = messageRoute.Receiver.IsMessageHub()
                                                                            ? messageRoute.Receiver.Identity
                                                                            : null,
                                                     MessageContract = messageRoute.Message != null
                                                                           ? new MessageContract
                                                                             {
                                                                                 Version = messageRoute.Message.Version,
                                                                                 Identity = messageRoute.Message.Identity,
                                                                                 Partition = messageRoute.Message.Partition
                                                                             }
                                                                           : null
                                                 })
                                         .As<Message>();
                    message.SetDomain(domain);
                    message.SignMessage(securityProvider);
                    autoDiscoverySender.EnqueueMessage(message);
                }
            }
            catch (SecurityException err)
            {
                logger.Error(err);
            }
        }

        public void RequestRouteDiscovery(MessageRoute messageRoute)
            => requests.TryEnqueue(messageRoute);
    }
}