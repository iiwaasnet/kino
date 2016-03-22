using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;

namespace kino.Core.Connectivity
{
    public class RouteDiscovery : IRouteDiscovery
    {
        private readonly IClusterMessageSender clusterMessageSender;
        private readonly RouterConfiguration routerConfiguration;
        private readonly RouteDiscoveryConfiguration discoveryConfiguration;
        private readonly ILogger logger;
        private readonly HashedQueue<MessageIdentifier> requests;
        private Task sendingMessages;
        private CancellationTokenSource cancellationTokenSource;

        public RouteDiscovery(IClusterMessageSender clusterMessageSender,
                              RouterConfiguration routerConfiguration,
                              ClusterMembershipConfiguration clusterMembershipConfiguration,
                              ILogger logger)
        {
            this.routerConfiguration = routerConfiguration;
            this.discoveryConfiguration = clusterMembershipConfiguration.RouteDiscovery ?? DefaultConfiguration();
            this.clusterMessageSender = clusterMessageSender;
            this.logger = logger;
            requests = new HashedQueue<MessageIdentifier>(this.discoveryConfiguration.MaxRequestsQueueLength);
        }

        public void Start()
        {
            cancellationTokenSource = new CancellationTokenSource();
            sendingMessages = Task.Factory.StartNew(_ => ThrottleRouteDiscoveryRequests(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            sendingMessages.Wait();
        }

        private async void ThrottleRouteDiscoveryRequests(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    IList<MessageIdentifier> missingRoutes;
                    if (requests.TryPeek(out missingRoutes, discoveryConfiguration.RequestsPerSend))
                    {
                        foreach (var messageIdentifier in missingRoutes)
                        {
                            var message = Message.Create(new DiscoverMessageRouteMessage
                                                         {
                                                             RequestorSocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                                             RequestorUri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                                             MessageContract = new MessageContract
                                                                               {
                                                                                   Version = messageIdentifier.Version,
                                                                                   Identity = messageIdentifier.Identity
                                                                               }
                                                         });
                            clusterMessageSender.EnqueueMessage(message);
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

        public void RequestRouteDiscovery(MessageIdentifier messageIdentifier)
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