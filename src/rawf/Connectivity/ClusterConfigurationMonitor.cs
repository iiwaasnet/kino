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
                    try
                    {
                        foreach (var messageOut in outgoingMessages.GetConsumingEnumerable(token))
                        {
                            sendingSocket.SendMessage(messageOut);
                            // TODO: Block immediatelly for the response
                            // Otherwise, consider the RS dead and switch to failover partner
                            //sendingSocket.ReceiveMessage(token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    sendingSocket.SendMessage(CreateUnregisterRoutingMessage());
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
                using (var subscriber = CreateClusterMonitorSubscriptionSocket())
                {
                    using (var routerNotificationSocket = CreateRouterCommunicationSocket())
                    {
                        gateway.SignalAndWait(token);

                        while (!token.IsCancellationRequested)
                        {
                            var message = subscriber.ReceiveMessage(token);
                            if (message != null)
                            {
                                ProcessIncomingMessage(message, routerNotificationSocket);
                            }
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
            socket.Subscribe();

            return socket;
        }

        private ISocket CreateClusterMonitorSendingSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(currentRendezvousServer.UnicastUri);

            return socket;
        }

        private ISocket CreateRouterCommunicationSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(routerConfiguration.RouterAddress.Uri);

            return socket;
        }

        private void ProcessIncomingMessage(IMessage message, ISocket routerNotificationSocket)
        {
            //TODO: Refactor message handlnig and forwarding
            var forwardMessageToRouter = true;
            
            if (IsRegisterExternalRoute(message))
            {
                AddClusterMember(message);
            }
            else if (IsPing(message))
            {
                SendPong();
                forwardMessageToRouter = false;                    
            }
            else if(IsPongNotFromSelf(message))
            {
                ProcessPongMessage(message);
                forwardMessageToRouter = false;
            }
            if (forwardMessageToRouter)
            {
                routerNotificationSocket.SendMessage(message);    
            }            
        }

        public void RegisterSelf(IEnumerable<MessageHandlerIdentifier> messageHandlers)
        {
            var message = Message.Create(new RegisterMessageHandlersRoutingMessage
                                         {
                                             Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
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

        private IMessage CreateUnregisterRoutingMessage()
            => Message.Create(new UnregisterMessageHandlersRoutingMessage
                              {
                                  Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                  SocketIdentity = routerConfiguration.ScaleOutAddress.Identity
                              },
                              UnregisterMessageHandlersRoutingMessage.MessageIdentity);
                              
        private bool IsRegisterExternalRoute(IMessage message) 
            => Unsafe.Equals(RegisterMessageHandlersRoutingMessage.MessageIdentity, message.Identity)                              
        
        private bool IsPing(IMessage message) 
            => Unsafe.Equals(PingMessage.MessageIdentity, message.Identity)
            
        private bool IsPongNotFromSelf(IMessage message)
        { 
            if (Unsafe.Equals(PongMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<PongMessage>();
                
                return payload.Uri.ToSocketAddress() != routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress()
                        && !Unsafe.Equals(payload.SocektIdentity, routerConfiguration.ScaleOutAddress.Identity);
            }
            
            return false;
        } 
            
        private void SendPong()
            => outgoingMessages.Add(Message.Create(new PongMessage
                    {
                        Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                        SocketIdentity = routerConfiguration.ScaleOutAddress.Identity
                    }, PongMessage.MessageIdentity));
                    
        private void AddClusterMember(IMessage message)
        {
            var registration = message.GetPayload<RegisterMessageHandlersRoutingMessage>();
            var clusterMember = new SocketEndpoint(new Uri(registration.Uri), registration.SocketIdentity);
            clusterConfiguration.AddClusterMember(clusterMember);
        }
        
        private void ProcessPongMessage(IMessage message)
        {
            var paylaod = message.GetPayload<PongMessage>();
            
            var updated = clusterConfiguration.KeepAlive(new SocketEndpoint(new Uri(payload.Uri, payload.SocketIdentity)));
            
            if (!updated)
            {
                
            }                        
        }                              
    }
}