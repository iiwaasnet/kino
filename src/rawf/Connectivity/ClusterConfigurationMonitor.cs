using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;

namespace rawf.Connectivity
{
    public class ClusterConfigurationMonitor : IClusterConfigurationMonitor
    {
        private readonly IConnectivityProvider connectivityProvider;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly BlockingCollection<IMessage> outgoingMessages;
        private Task sendingMessages;
        private Task listenningMessages;

        public ClusterConfigurationMonitor(IConnectivityProvider connectivityProvider)
        {
            this.connectivityProvider = connectivityProvider;
            outgoingMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            var participantCount = 3;
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
                using (var sendingSocket = connectivityProvider.CreateRendezvousSendingSocket())
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
                using (var subscriber = connectivityProvider.CreateRendezvousSubscriptionSocket())
                {
                    gateway.SignalAndWait(token);

                    while (!token.IsCancellationRequested)
                    {
                        var message = subscriber.ReceiveMessage(token);
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        public void RegisterMember(ClusterMember member, IEnumerable<MessageHandlerIdentifier> messageHandlers)
        {
            var message = Message.Create(new RouteRegistrationMessage
                                         {
                                             Uri = member.Uri.ToSocketAddress(),
                                             SocketIdentity = member.Identity,
                                             Registrations = messageHandlers.Select(mh => new MessageRegistration
                                                                                          {
                                                                                              Version = mh.Version,
                                                                                              Identity = mh.Identity
                                                                                          }).ToArray()
                                         },
                                         RouteRegistrationMessage.MessageIdentity);
            outgoingMessages.Add(message);
        }

        public void UnregisterMember(ClusterMember member)
        {
            throw new NotImplementedException();
        }
    }
}