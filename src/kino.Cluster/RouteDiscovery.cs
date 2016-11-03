using System;
using System.Collections.Generic;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using kino.Configuration;
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
        private readonly HashedQueue<Identifier> requests;
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
            requests = new HashedQueue<Identifier>(discoveryConfiguration.MaxRequestsQueueLength);
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
                    IList<Identifier> missingRoutes;
                    if (requests.TryPeek(out missingRoutes, discoveryConfiguration.RequestsPerSend))
                    {
                        foreach (var messageIdentifier in missingRoutes)
                        {
                            SendDiscoverMessageRouteMessage(messageIdentifier);
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

        private void SendDiscoverMessageRouteMessage(Identifier messageIdentifier)
        {
            try
            {
                var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
                var domains = messageIdentifier.IsMessageHub()
                                  ? securityProvider.GetAllowedDomains()
                                  : new[] {securityProvider.GetDomain(messageIdentifier.Identity)};
                foreach (var domain in domains)
                {
                    var message = Message.Create(new DiscoverMessageRouteMessage
                                                 {
                                                     RequestorSocketIdentity = scaleOutAddress.Identity,
                                                     RequestorUri = scaleOutAddress.Uri.ToSocketAddress(),
                                                     MessageContract = new MessageContract
                                                                       {
                                                                           Version = messageIdentifier.Version,
                                                                           Identity = messageIdentifier.Identity,
                                                                           Partition = messageIdentifier.Partition,
                                                                           IsAnyIdentifier = messageIdentifier is AnyIdentifier
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

        public void RequestRouteDiscovery(Identifier messageIdentifier)
            => requests.TryEnqueue(messageIdentifier);

        private RouteDiscoveryConfiguration DefaultConfiguration()
            => new RouteDiscoveryConfiguration
               {
                   SendingPeriod = TimeSpan.FromSeconds(2),
                   MaxRequestsQueueLength = 1000,
                   RequestsPerSend = 10
               };
    }
}