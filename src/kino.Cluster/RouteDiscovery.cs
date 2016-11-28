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
            discoveryConfiguration = clusterMembershipConfiguration.RouteDiscovery ?? DefaultConfiguration();
            this.autoDiscoverySender = autoDiscoverySender;
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.logger = logger;
            requests = new HashedQueue<MessageRoute>(discoveryConfiguration.MaxRequestsQueueLength);
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

        private async void ThrottleRouteDiscoveryRequests(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    IList<MessageRoute> missingRoutes;
                    if (requests.TryPeek(out missingRoutes, discoveryConfiguration.RequestsPerSend))
                    {
                        foreach (var messageRoute in missingRoutes)
                        {
                            SendDiscoverMessageRouteMessage(messageRoute);
                        }
                    }

                    await Task.Delay(discoveryConfiguration.SendingPeriod, token);

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
                                  : new[] {securityProvider.GetDomain(messageRoute.Message)};
                foreach (var domain in domains)
                {
                    var message = Message.Create(new DiscoverMessageRouteMessage
                                                 {
                                                     RequestorSocketIdentity = scaleOutAddress.Identity,
                                                     RequestorUri = scaleOutAddress.Uri.ToSocketAddress(),
                                                     MessageContract = new MessageContract
                                                                       {
                                                                           Version = messageRoute.Version,
                                                                           Identity = messageRoute.Identity,
                                                                           Partition = messageRoute.Partition,
                                                                           IsAnyIdentifier = messageRoute is AnyIdentifier
                                                                       }
                                                 },
                                                 domain);
                    message.As<Message>().SignMessage(securityProvider);
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

        private RouteDiscoveryConfiguration DefaultConfiguration()
            => new RouteDiscoveryConfiguration
               {
                   SendingPeriod = TimeSpan.FromSeconds(2),
                   MaxRequestsQueueLength = 1000,
                   RequestsPerSend = 10
               };
    }
}