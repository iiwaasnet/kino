using System;
using System.Linq;
using System.Threading;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;
using kino.Tests.Actors.Setup;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace kino.Tests.Cluster
{
    public class ClusterMonitorTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(100);
        private readonly Mock<ILogger> logger;
        private readonly Mock<IRouteDiscovery> routeDiscovery;
        private readonly Mock<ISecurityProvider> securityProvider;
        private readonly string domain;
        private readonly ClusterMonitor clusterMonitor;
        private readonly SocketEndpoint scaleOutAddress;
        private readonly Mock<IScaleOutConfigurationProvider> scaleOutConfigurationProvider;
        private readonly Mock<IAutoDiscoverySender> autoDiscoverySender;
        private readonly Mock<IAutoDiscoveryListener> autoDiscoveryListener;
        private readonly Mock<IHeartBeatSenderConfigurationProvider> heartBeatSenderConfigProvider;
        private readonly Uri heartBeatUri;
        private readonly TimeSpan heartBeatInterval;
        private readonly ClusterMembershipConfiguration config;

        public ClusterMonitorTests()
        {
            securityProvider = new Mock<ISecurityProvider>();
            domain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            logger = new Mock<ILogger>();
            routeDiscovery = new Mock<IRouteDiscovery>();
            scaleOutAddress = new SocketEndpoint(new Uri("tcp://127.0.0.1:5000"), Guid.NewGuid().ToByteArray());
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            autoDiscoverySender = new Mock<IAutoDiscoverySender>();
            autoDiscoveryListener = new Mock<IAutoDiscoveryListener>();
            heartBeatSenderConfigProvider = new Mock<IHeartBeatSenderConfigurationProvider>();
            heartBeatUri = new Uri("tcp://127.0.0.1:890");
            heartBeatSenderConfigProvider.Setup(m => m.GetHeartBeatAddress()).Returns(heartBeatUri);
            heartBeatInterval = TimeSpan.FromSeconds(5);
            heartBeatSenderConfigProvider.Setup(m => m.GetHeartBeatInterval()).Returns(heartBeatInterval);
            config = new ClusterMembershipConfiguration
                     {
                         RouteDiscovery = new RouteDiscoveryConfiguration
                                          {
                                              ClusterAutoDiscoveryStartDelay = TimeSpan.FromSeconds(1),
                                              ClusterAutoDiscoveryPeriod = TimeSpan.FromSeconds(2),
                                              ClusterAutoDiscoveryStartDelayMaxMultiplier = 2,
                                              MaxAutoDiscoverySenderQueueLength = 100
                                          }
                     };
            clusterMonitor = new ClusterMonitor(scaleOutConfigurationProvider.Object,
                                                autoDiscoverySender.Object,
                                                autoDiscoveryListener.Object,
                                                heartBeatSenderConfigProvider.Object,
                                                routeDiscovery.Object,
                                                securityProvider.Object,
                                                config,
                                                logger.Object);
        }

        [Fact]
        public void RegisterSelf_SendsRegistrationMessageForEachReceiver()
        {
            var actorIdentifier = ReceiverIdentities.CreateForActor();
            var messageHubIdentifier = ReceiverIdentities.CreateForMessageHub();
            var registrations = new[]
                                {
                                    new MessageRoute
                                    {
                                        Message = MessageIdentifier.Create<SimpleMessage>(),
                                        Receiver = actorIdentifier
                                    },
                                    new MessageRoute
                                    {
                                        Message = MessageIdentifier.Create<ExceptionMessage>(),
                                        Receiver = actorIdentifier
                                    },
                                    new MessageRoute
                                    {
                                        Receiver = messageHubIdentifier
                                    }
                                };
            //
            clusterMonitor.RegisterSelf(registrations, domain);
            //
            Func<IMessage, bool> messageIsConsistent = msg =>
                                                       {
                                                           if (msg.Equals(MessageIdentifier.Create<RegisterExternalMessageRouteMessage>()))
                                                           {
                                                               var payload = msg.GetPayload<RegisterExternalMessageRouteMessage>();
                                                               Assert.Equal(payload.Uri, scaleOutAddress.Uri.ToSocketAddress());
                                                               Assert.True(Unsafe.ArraysEqual(payload.NodeIdentity, scaleOutAddress.Identity));
                                                               Assert.Equal(payload.Health.Uri, heartBeatUri.ToSocketAddress());
                                                               Assert.Equal(payload.Health.HeartBeatInterval, heartBeatInterval);
                                                               var actorRoutes = payload.Routes
                                                                                        .First(r => Unsafe.ArraysEqual(r.ReceiverIdentity, actorIdentifier.Identity));
                                                               foreach (var registration in registrations.Where(r => r.Receiver == actorIdentifier))
                                                               {
                                                                   Assert.True(actorRoutes.MessageContracts.Any(mc => Unsafe.ArraysEqual(mc.Identity, registration.Message.Identity)
                                                                                                                      && Unsafe.ArraysEqual(mc.Partition, registration.Message.Partition)
                                                                                                                      && mc.Version == registration.Message.Version));
                                                               }
                                                               var messageHub = payload.Routes
                                                                                       .First(r => Unsafe.ArraysEqual(r.ReceiverIdentity, messageHubIdentifier.Identity));
                                                               Assert.Null(messageHub.MessageContracts);
                                                               return true;
                                                           }

                                                           return false;
                                                       };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => messageIsConsistent(msg))), Times.Once);
        }

        [Fact]
        public void RegisterSelf_SendsOneRegistrationMessageForSpecifiedDomainButNotMessageDomain()
        {
            var actorIdentifier = ReceiverIdentities.CreateForActor();
            var registrations = new[]
                                {
                                    new MessageRoute
                                    {
                                        Message = MessageIdentifier.Create<SimpleMessage>(),
                                        Receiver = actorIdentifier
                                    },
                                    new MessageRoute
                                    {
                                        Message = MessageIdentifier.Create<ExceptionMessage>(),
                                        Receiver = actorIdentifier
                                    }
                                };
            var simpleMessageDomain = Guid.NewGuid().ToString();
            var exceptionMessageDomain = Guid.NewGuid().ToString();
            var allowedDomains = new[] {exceptionMessageDomain, simpleMessageDomain, Guid.NewGuid().ToString()};
            securityProvider.Setup(m => m.GetDomain(MessageIdentifier.Create<SimpleMessage>().Identity)).Returns(simpleMessageDomain);
            securityProvider.Setup(m => m.GetDomain(MessageIdentifier.Create<ExceptionMessage>().Identity)).Returns(exceptionMessageDomain);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            //
            clusterMonitor.RegisterSelf(registrations, domain);
            //
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => msg.Domain == domain)), Times.Once);
        }

        [Fact]
        public void UnregisterSelf_SendsOneUnregisterMessageRouteMessagePerDomain()
        {
            var actorIdentifier = ReceiverIdentities.CreateForActor();
            var messageHubIdentifier = ReceiverIdentities.CreateForMessageHub();
            var routes = new[]
                         {
                             new MessageRoute
                             {
                                 Message = MessageIdentifier.Create<SimpleMessage>(),
                                 Receiver = actorIdentifier
                             },
                             new MessageRoute
                             {
                                 Message = MessageIdentifier.Create<ExceptionMessage>(),
                                 Receiver = actorIdentifier
                             },
                             new MessageRoute
                             {
                                 Receiver = messageHubIdentifier
                             }
                         };
            var simpleMessageDomain = Guid.NewGuid().ToString();
            var exceptionMessageDomain = Guid.NewGuid().ToString();
            var allowedDomains = new[] {exceptionMessageDomain, simpleMessageDomain, Guid.NewGuid().ToString()};
            securityProvider.Setup(m => m.GetDomain(MessageIdentifier.Create<SimpleMessage>().Identity)).Returns(simpleMessageDomain);
            securityProvider.Setup(m => m.GetDomain(MessageIdentifier.Create<ExceptionMessage>().Identity)).Returns(exceptionMessageDomain);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            //
            clusterMonitor.UnregisterSelf(routes);
            //
            Func<IMessage, bool> messageIsConsistent = msg =>
                                                       {
                                                           if (msg.Equals(MessageIdentifier.Create<UnregisterMessageRouteMessage>()))
                                                           {
                                                               var payload = msg.GetPayload<UnregisterMessageRouteMessage>();
                                                               Assert.Equal(payload.Uri, scaleOutAddress.Uri.ToSocketAddress());
                                                               Assert.True(Unsafe.ArraysEqual(payload.ReceiverNodeIdentity, scaleOutAddress.Identity));
                                                               Assert.True(allowedDomains.Contains(msg.Domain));
                                                               return true;
                                                           }

                                                           return false;
                                                       };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => messageIsConsistent(msg))), Times.Exactly(allowedDomains.Length));
        }

        [Fact]
        public void UnregisterSelfForMessagesWithoutReceiver_GroupsMessagesForSendingWithoutException()
        {
            var unregRoutes = new[]
                              {
                                  new MessageRoute
                                  {
                                      Message = MessageIdentifier.Create<SimpleMessage>(),
                                      Receiver = null
                                  },
                                  new MessageRoute
                                  {
                                      Message = MessageIdentifier.Create<ExceptionMessage>(),
                                      Receiver = null
                                  },
                                  new MessageRoute
                                  {
                                      Message = MessageIdentifier.Create<AsyncExceptionMessage>(),
                                      Receiver = null
                                  }
                              };
            //
            clusterMonitor.UnregisterSelf(unregRoutes);
            //
            Func<IMessage, bool> messageIsConsistent = msg =>
                                                       {
                                                           if (msg.Equals(MessageIdentifier.Create<UnregisterMessageRouteMessage>()))
                                                           {
                                                               var payload = msg.GetPayload<UnregisterMessageRouteMessage>();
                                                               Assert.Equal(payload.Uri, scaleOutAddress.Uri.ToSocketAddress());
                                                               Assert.True(Unsafe.ArraysEqual(payload.ReceiverNodeIdentity, scaleOutAddress.Identity));
                                                               Assert.Equal(domain, msg.Domain);
                                                               Assert.Equal(1, payload.Routes.Length);
                                                               Assert.Equal(payload.Routes.First().MessageContracts.Length, unregRoutes.Length);
                                                               Assert.True(payload.Routes.All(r => r.ReceiverIdentity == null));
                                                               return true;
                                                           }

                                                           return false;
                                                       };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => messageIsConsistent(msg))), Times.Once);
        }

        [Fact]
        public void WhenClusterMonitorStarts_ClusterRoutesAreRequested()
        {
            Func<Barrier, bool> setBarrier = (b) =>
                                             {
                                                 b.SignalAndWait();
                                                 return true;
                                             };
            autoDiscoveryListener.Setup(m => m.StartBlockingListenMessages(It.IsAny<Action>(),
                                                                           It.IsAny<CancellationToken>(),
                                                                           It.Is<Barrier>(b => setBarrier(b))));
            autoDiscoverySender.Setup(m => m.StartBlockingSendMessages(It.IsAny<CancellationToken>(),
                                                                       It.Is<Barrier>(b => setBarrier(b))));
            //
            clusterMonitor.Start();
            config.RouteDiscovery
                  .ClusterAutoDiscoveryStartDelay
                  .MultiplyBy(config.RouteDiscovery.ClusterAutoDiscoveryStartDelayMaxMultiplier)
                  .Sleep();
            clusterMonitor.Stop();
            //
            Func<Message, bool> isRequestClusterRoutesMessage = (msg) =>
                                                                {
                                                                    if (msg.Equals(MessageIdentifier.Create<RequestClusterMessageRoutesMessage>()))
                                                                    {
                                                                        var payload = msg.GetPayload<RequestClusterMessageRoutesMessage>();
                                                                        Assert.True(Unsafe.ArraysEqual(scaleOutAddress.Identity, payload.RequestorNodeIdentity));
                                                                        Assert.Equal(scaleOutAddress.Uri.ToSocketAddress(), payload.RequestorUri);
                                                                        return true;
                                                                    }

                                                                    return false;
                                                                };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isRequestClusterRoutesMessage(msg.As<Message>()))), Times.Once);
        }

        [Fact]
        public void WhenCusterMonitorStops_UnregisterNodeMessageIsSent()
        {
            Func<Barrier, bool> setBarrier = (b) =>
                                             {
                                                 b.SignalAndWait();
                                                 return true;
                                             };
            autoDiscoveryListener.Setup(m => m.StartBlockingListenMessages(It.IsAny<Action>(),
                                                                           It.IsAny<CancellationToken>(),
                                                                           It.Is<Barrier>(b => setBarrier(b))));
            autoDiscoverySender.Setup(m => m.StartBlockingSendMessages(It.IsAny<CancellationToken>(),
                                                                       It.Is<Barrier>(b => setBarrier(b))));
            //
            clusterMonitor.Start();
            AsyncOp.Sleep();
            clusterMonitor.Stop();
            config.RouteDiscovery
                  .ClusterAutoDiscoveryStartDelay
                  .MultiplyBy(config.RouteDiscovery.ClusterAutoDiscoveryStartDelayMaxMultiplier)
                  .Sleep();
            //
            Func<IMessage, bool> isUnregistrationMessage = msg =>
                                                           {
                                                               if (msg.Equals(MessageIdentifier.Create<UnregisterNodeMessage>()))
                                                               {
                                                                   var payload = msg.GetPayload<UnregisterNodeMessage>();
                                                                   Assert.True(Unsafe.ArraysEqual(scaleOutAddress.Identity, payload.ReceiverNodeIdentity));
                                                                   Assert.Equal(scaleOutAddress.Uri.ToSocketAddress(), payload.Uri);
                                                                   return true;
                                                               }

                                                               return false;
                                                           };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isUnregistrationMessage(msg))), Times.Once);
        }

        [Fact]
        public void ClusterMonitor_PeriodicallySendsClusterRoutesRequests()
        {
            Func<Barrier, bool> setBarrier = (b) =>
                                             {
                                                 b.SignalAndWait();
                                                 return true;
                                             };
            autoDiscoveryListener.Setup(m => m.StartBlockingListenMessages(It.IsAny<Action>(),
                                                                           It.IsAny<CancellationToken>(),
                                                                           It.Is<Barrier>(b => setBarrier(b))));
            autoDiscoverySender.Setup(m => m.StartBlockingSendMessages(It.IsAny<CancellationToken>(),
                                                                       It.Is<Barrier>(b => setBarrier(b))));
            var numberOfPeriods = 3;
            //
            clusterMonitor.Start();
            config.RouteDiscovery
                  .ClusterAutoDiscoveryPeriod
                  .MultiplyBy(numberOfPeriods)
                  .Sleep();
            clusterMonitor.Start();
            //
            Func<Message, bool> isRequestClusterRoutesMessage = (msg) =>
                                                                {
                                                                    if (msg.Equals(MessageIdentifier.Create<RequestClusterMessageRoutesMessage>()))
                                                                    {
                                                                        var payload = msg.GetPayload<RequestClusterMessageRoutesMessage>();
                                                                        Assert.True(Unsafe.ArraysEqual(scaleOutAddress.Identity, payload.RequestorNodeIdentity));
                                                                        Assert.Equal(scaleOutAddress.Uri.ToSocketAddress(), payload.RequestorUri);
                                                                        return true;
                                                                    }

                                                                    return false;
                                                                };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isRequestClusterRoutesMessage(msg.As<Message>()))), Times.AtLeast(numberOfPeriods));
        }
    }
}