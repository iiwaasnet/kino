using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public class ClusterConfigurationMonitor : IClusterConfigurationMonitor
    {
        private readonly ISocketFactory socketFactory;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly BlockingCollection<IMessage> outgoingMessages;
        private readonly IClusterConfiguration clusterConfiguration;
        private readonly IRouterConfiguration routerConfiguration;
        private readonly RendezvousServerConfiguration currentRendezvousServer;
        private Task sendingMessages;
        private Task listenningMessages;

        public ClusterConfigurationMonitor(ISocketFactory socketFactory,
                                           IRouterConfiguration routerConfiguration,
                                           IClusterConfiguration clusterConfiguration,
                                           IRendezvousConfiguration rendezvousConfiguration)
        {
            this.socketFactory = socketFactory;
            this.routerConfiguration = routerConfiguration;
            this.clusterConfiguration = clusterConfiguration;
            outgoingMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            cancellationTokenSource = new CancellationTokenSource();
            currentRendezvousServer = rendezvousConfiguration.GetRendezvousServers().First();
        }

        public void Start()
        {
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                sendingMessages = Task.Factory.StartNew(_ => SendMessages(cancellationTokenSource.Token, gateway),
                                                        TaskCreationOptions.LongRunning);
                listenningMessages = Task.Factory.StartNew(_ => ListenMessages(cancellationTokenSource.Token, gateway),
                                                           TaskCreationOptions.LongRunning);

                gateway.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            sendingMessages.Wait();
            listenningMessages.Wait();
        }

        private void SendMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var sendingSocket = CreateClusterMonitorSendingSocket())
                {
                    gateway.SignalAndWait(token);

                    foreach (var messageOut in outgoingMessages.GetConsumingEnumerable(token))
                    {
                        sendingSocket.SendMessage(messageOut);
                        // TODO: Block immediatelly for the response
                        // Otherwise, consider the RS dead and switch to failover partner
                        //sendingSocket.ReceiveMessage(token);
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private ISocket CreateClusterMonitorSendingSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(currentRendezvousServer.UnicastUri);

            return socket;
        }

        private void ListenMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var subscriber = CreateClusterMonitorSubscriptionSocket())
                {
                    using (var routerNotificationSocket = CreateOneWaySocket())
                    {
                        gateway.SignalAndWait(token);

                        while (!token.IsCancellationRequested)
                        {
                            var message = subscriber.ReceiveMessage(token);
                            ProcessIncomingMessage(message, routerNotificationSocket);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private ISocket CreateClusterMonitorSubscriptionSocket()
        {
            var socket = socketFactory.CreateSubscriberSocket();
            socket.Connect(currentRendezvousServer.BroadcastUri);

            return socket;
        }

        private ISocket CreateOneWaySocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(routerConfiguration.RouterAddress.Uri);

            return socket;
        }

        private void ProcessIncomingMessage(IMessage message, ISocket routerNotificationSocket)
        {
            if (Unsafe.Equals(RegisterMessageHandlersRoutingMessage.MessageIdentity, message.Identity))
            {
                var registration = message.GetPayload<RegisterMessageHandlersRoutingMessage>();
                var clusterMember = new SocketEndpoint(new Uri(registration.Uri), registration.SocketIdentity);
                clusterConfiguration.AddClusterMember(clusterMember);
                routerNotificationSocket.SendMessage(message);
            }
        }

        public void RegisterSelf(IEnumerable<MessageHandlerIdentifier> messageHandlers)
        {
            var self = new SocketEndpoint(routerConfiguration.ScaleOutAddress.Uri,
                                         routerConfiguration.ScaleOutAddress.Identity);

            var message = Message.Create(new RegisterMessageHandlersRoutingMessage
                                         {
                                             Uri = self.Uri.ToSocketAddress(),
                                             SocketIdentity = self.Identity,
                                             MessageHandlers = messageHandlers.Select(mh => new MessageHandlerRegistration
                                                                                            {
                                                                                                Version = mh.Version,
                                                                                                Identity = mh.Identity
                                                                                            }).ToArray()
                                         },
                                         RegisterMessageHandlersRoutingMessage.MessageIdentity);
            outgoingMessages.Add(message);
        }

        public void RequestMessageHandlersRouting()
        {
            var message = Message.Create(new RequestMessageHandlersRoutingMessage
                                         {
                                             RequestorSocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             RequestorUri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress()
                                         },
                                         RequestMessageHandlersRoutingMessage.MessageIdentity);
            outgoingMessages.Add(message);
        }

        public void UnregisterMember(SocketEndpoint member)
        {
            throw new NotImplementedException();
        }
    }
}