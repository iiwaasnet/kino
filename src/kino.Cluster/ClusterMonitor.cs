using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Cluster
{
    public class ClusterMonitor : IClusterMonitor
    {
        private CancellationTokenSource messageProcessingToken;
        private Task sendingMessages;
        private Task listenningMessages;
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly IAutoDiscoverySender autoDiscoverySender;
        private readonly IAutoDiscoveryListener autoDiscoveryListener;
        private readonly IHeartBeatSenderConfigurationProvider heartBeatConfigurationProvider;
        private readonly IRouteDiscovery routeDiscovery;
        private readonly ISecurityProvider securityProvider;

        public ClusterMonitor(IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                              IAutoDiscoverySender autoDiscoverySender,
                              IAutoDiscoveryListener autoDiscoveryListener,
                              IHeartBeatSenderConfigurationProvider heartBeatConfigurationProvider,
                              IRouteDiscovery routeDiscovery,
                              ISecurityProvider securityProvider)
        {
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.autoDiscoverySender = autoDiscoverySender;
            this.autoDiscoveryListener = autoDiscoveryListener;
            this.heartBeatConfigurationProvider = heartBeatConfigurationProvider;
            this.routeDiscovery = routeDiscovery;
            this.securityProvider = securityProvider;
        }

        public void Start()
            => StartProcessingClusterMessages();

        public void Stop()
            => StopProcessingClusterMessages();

        private void StartProcessingClusterMessages()
        {
            messageProcessingToken = new CancellationTokenSource();
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                sendingMessages = Task.Factory.StartNew(_ => autoDiscoverySender.StartBlockingSendMessages(messageProcessingToken.Token, gateway),
                                                        TaskCreationOptions.LongRunning);
                listenningMessages = Task.Factory.StartNew(_ => autoDiscoveryListener.StartBlockingListenMessages(RestartProcessingClusterMessages, messageProcessingToken.Token, gateway),
                                                           TaskCreationOptions.LongRunning);
                gateway.SignalAndWait(messageProcessingToken.Token);

                routeDiscovery.Start();
            }
        }

        private void StopProcessingClusterMessages()
        {
            routeDiscovery.Stop();
            messageProcessingToken?.Cancel();
            sendingMessages?.Wait();
            listenningMessages?.Wait();
            messageProcessingToken?.Dispose();
        }

        private void RestartProcessingClusterMessages()
        {
            StopProcessingClusterMessages();
            StartProcessingClusterMessages();
        }

        public void RegisterSelf(IEnumerable<MessageRoute> registrations, string domain)
        {
            var receiverRoutes = registrations.GroupBy(r => r.Receiver)
                                              .Select(g => new
                                                           {
                                                               ReceiverIdentifier = g.Key.Identity,
                                                               Messages = g.Key.IsActor()
                                                                              ? g.Select(x => x.Message)
                                                                              : Enumerable.Empty<MessageIdentifier>()
                                                           });
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();

            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = scaleOutAddress.Uri.ToSocketAddress(),
                                             NodeIdentity = scaleOutAddress.Identity,
                                             Health = new Messaging.Messages.Health
                                                      {
                                                          Uri = heartBeatConfigurationProvider.GetHeartBeatAddress()
                                                                                              .ToSocketAddress(),
                                                          HeartBeatInterval = heartBeatConfigurationProvider.GetHeartBeatInterval()
                                                      },
                                             Routes = receiverRoutes.Select(r => new RouteRegistration
                                                                                 {
                                                                                     ReceiverIdentity = r.ReceiverIdentifier,
                                                                                     MessageContracts = r.Messages.Select(m => new MessageContract
                                                                                                                               {
                                                                                                                                   Identity = m.Identity,
                                                                                                                                   Partition = m.Partition,
                                                                                                                                   Version = m.Version
                                                                                                                               })
                                                                                                         .ToArray()
                                                                                 }).ToArray()
                                         },
                                         domain);
            message.As<Message>().SignMessage(securityProvider);
            autoDiscoverySender.EnqueueMessage(message);
        }

        public void UnregisterSelf(IEnumerable<MessageRoute> messageRoutes)
        {
            //TODO: MessageRoute.Receiver can be NULL. Check how grouping will work in this case
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
            var routeByDomain = GetMessageHubs(messageRoutes).Concat(GetActors(messageRoutes))
                                                             .GroupBy(mr => mr.Domain)
                                                             .Select(g => new
                                                                          {
                                                                              Domain = g.Key,
                                                                              MessageRoutes = g.GroupBy(mr => mr.MessageRoute.Receiver)
                                                                                               .Select(rg => new
                                                                                                             {
                                                                                                                 Receiver = rg.Key,
                                                                                                                 MessageContracts = rg.Select(x => x.MessageRoute.Message)
                                                                                                             })
                                                                          });

            foreach (var domainRoutes in routeByDomain)
            {
                var message = Message.Create(new UnregisterMessageRouteMessage
                                             {
                                                 Uri = scaleOutAddress.Uri.ToSocketAddress(),
                                                 ReceiverNodeIdentity = scaleOutAddress.Identity,
                                                 Routes = domainRoutes.MessageRoutes
                                                                      .Select(r => new RouteRegistration
                                                                                   {
                                                                                       ReceiverIdentity = r.Receiver?.Identity,
                                                                                       MessageContracts = r.MessageContracts
                                                                                                           ?.Select(mc => new MessageContract
                                                                                                                          {
                                                                                                                              Identity = mc.Identity,
                                                                                                                              Version = mc.Version,
                                                                                                                              Partition = mc.Partition
                                                                                                                          })
                                                                                                           .ToArray()
                                                                                   })
                                                                      .ToArray()
                                             },
                                             domainRoutes.Domain);
                message.As<Message>().SignMessage(securityProvider);

                autoDiscoverySender.EnqueueMessage(message);
            }
        }

        private IEnumerable<MessageRouteDomainMap> GetActors(IEnumerable<MessageRoute> messageRoutes)
            => messageRoutes.Where(mr => !mr.Receiver.IsSet() || mr.Receiver.IsActor())
                            .Select(mr => new MessageRouteDomainMap
                                          {
                                              MessageRoute = mr,
                                              Domain = securityProvider.GetDomain(mr.Message.Identity)
                                          });

        private IEnumerable<MessageRouteDomainMap> GetMessageHubs(IEnumerable<MessageRoute> messageRoutes)
            => messageRoutes.Where(mr => mr.Receiver.IsMessageHub())
                            .SelectMany(mr => securityProvider.GetAllowedDomains()
                                                              .Select(dom =>
                                                                          new MessageRouteDomainMap
                                                                          {
                                                                              MessageRoute = mr,
                                                                              Domain = dom
                                                                          }));

        public void DiscoverMessageRoute(MessageRoute messageRoute)
            => routeDiscovery.RequestRouteDiscovery(messageRoute);
    }
}