using System;
using System.Threading;
using System.Threading.Tasks;
using rawf.Sockets;
using TypedConfigProvider;

namespace rawf.Rendezvous
{
    public class RendezvousService : IRendezvousService
    {
        private readonly ISocketFactory socketFactory;
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task messageProcessing;
        private readonly ApplicationConfiguration config;

        public RendezvousService(ISocketFactory socketFactory, IConfigProvider configProvider)
        {
            this.socketFactory = socketFactory;
            config = configProvider.GetConfiguration<ApplicationConfiguration>();
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            const int participantCount = 2;
            using (var gateway = new Barrier(participantCount))
            {
                messageProcessing = Task.Factory.StartNew(_ => ProcessMessages(cancellationTokenSource.Token, gateway),
                                                          TaskCreationOptions.LongRunning);

                gateway.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        private void ProcessMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var broadcastSocket = CreateBroadcastSocket())
                {
                    using (var unicastSocket = CreateUnicastSocket())
                    {
                        gateway.SignalAndWait(token);
                        while (!token.IsCancellationRequested)
                        {
                            var message = unicastSocket.ReceiveMessage(token);
                            if (message != null)
                            {
                                broadcastSocket.SendMessage(message);
                            }
                        }
                    }
                }
            }
            catch (Exception err) when (!(err is OperationCanceledException))
            {
                Console.WriteLine(err);
            }
        }

        private ISocket CreateUnicastSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetIdentity(config.UnicastSocketIdentity);
            socket.Bind(new Uri(config.UnicastUri));

            return socket;
        }

        private ISocket CreateBroadcastSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.Bind(new Uri(config.BroadcastUri));

            return socket;
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            messageProcessing.Wait();
        }
    }
}