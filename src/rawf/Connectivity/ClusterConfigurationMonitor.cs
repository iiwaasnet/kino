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
        private readonly IConnectivityProvider connectivityProvider;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly BlockingCollection<IMessage> outgoingMessages;
        private readonly IClusterConfiguration clusterConfiguration;
        private readonly INodeConfiguration nodeConfiguration;
        private Task sendingMessages;
        private Task listenningMessages;

        public ClusterConfigurationMonitor(IConnectivityProvider connectivityProvider,
                                           INodeConfiguration nodeConfiguration,
                                           IClusterConfiguration clusterConfiguration)
        {
            this.connectivityProvider = connectivityProvider;
            this.nodeConfiguration = nodeConfiguration;
            outgoingMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            cancellationTokenSource = new CancellationTokenSource();
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
            throw new NotImplementedException();
        }

        private void SendMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var sendingSocket = connectivityProvider.CreateClusterMonitorSendingSocket())
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

        private void ListenMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var subscriber = connectivityProvider.CreateClusterMonitorSubscriptionSocket())
                {
                    using (var routerNotificationSocket = connectivityProvider.CreateOneWaySocket())
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

        private void ProcessIncomingMessage(IMessage message, ISocket routerNotificationSocket)
        {
            if (Unsafe.Equals(RegisterMessageHandlersRoutingMessage.MessageIdentity, message.Identity))
            {
                var registration = message.GetPayload<RegisterMessageHandlersRoutingMessage>();
                clusterConfiguration.TryAddClusterMember(new ClusterMember
                                                         {
                                                             Uri = new Uri(registration.Uri),
                                                             Identity = registration.SocketIdentity
                                                         });
                routerNotificationSocket.SendMessage(message);
            }
        }

        public void RegisterSelf(IEnumerable<MessageHandlerIdentifier> messageHandlers)
        {
            var self = new ClusterMember
                       {
                           Uri = nodeConfiguration.ScaleOutAddress.Uri,
                           Identity = nodeConfiguration.ScaleOutAddress.Identity
                       };

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
                                             RequestorSocketIdentity = nodeConfiguration.ScaleOutAddress.Identity,
                                             RequestorUri = nodeConfiguration.ScaleOutAddress.Uri.ToSocketAddress()
                                         },
                                         RequestMessageHandlersRoutingMessage.MessageIdentity);
            outgoingMessages.Add(message);
        }

        public void UnregisterMember(ClusterMember member)
        {
            throw new NotImplementedException();
        }
    }
}