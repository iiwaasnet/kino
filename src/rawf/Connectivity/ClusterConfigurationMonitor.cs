using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace rawf.Connectivity
{
    public class ClusterConfigurationMonitor : IClusterConfigurationMonitor
    {
        private readonly IConnectivityProvider connectivityProvider;
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task sendingMessages;
        private Task listenningMessages; 

        public ClusterConfigurationMonitor(IConnectivityProvider connectivityProvider)
        {
            this.connectivityProvider = connectivityProvider;
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
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private void ListenMessages(CancellationToken token, Barrier gateway)
        {
        }

        public void RegisterMember(ClusterMember member, IEnumerable<MessageHandlerIdentifier> messageHandlers)
        {
            throw new NotImplementedException();
        }

        public void UnregisterMember(ClusterMember member)
        {
            throw new NotImplementedException();
        }
    }
}