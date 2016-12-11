using System;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using NetMQ;

namespace kino.Cluster
{
    public class HeartBeatSender : IHeartBeatSender
    {
        private readonly ISocketFactory socketFactory;
        private readonly IHeartBeatSenderConfigurationManager config;
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly ILogger logger;
        private CancellationTokenSource cancellationTokenSource;
        private Task heartBeating;

        public HeartBeatSender(ISocketFactory socketFactory,
                               IHeartBeatSenderConfigurationManager config,
                               IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                               ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.config = config;
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.logger = logger;
        }

        public void Start()
        {
            cancellationTokenSource = new CancellationTokenSource();

            heartBeating = Task.Factory.StartNew(_ => SendHeartBeat(cancellationTokenSource.Token), TaskCreationOptions.LongRunning, cancellationTokenSource.Token);
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            heartBeating?.Wait();
            cancellationTokenSource?.Dispose();
        }

        private async void SendHeartBeat(CancellationToken token)
        {
            try
            {
                using (var socket = CreateHeartBeatSocket())
                {
                    var heartBeatMessage = new HeartBeatMessage
                                           {
                                               SocketIdentity = scaleOutConfigurationProvider.GetScaleOutAddress().Identity,
                                               HeartBeatInterval = config.GetHeartBeatInterval()
                                           };
                    while (!token.IsCancellationRequested)
                    {
                        socket.SendMessage(Message.Create(heartBeatMessage));
                        //logger.Debug($"HeartBeat sent at {DateTime.UtcNow} UTC");
                        //TODO: Allow cancellation of Sleep() with token
                        config.GetHeartBeatInterval().Sleep();
                        //await Task.Delay(config.GetHeartBeatInterval(), token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                logger.Error($"HeartBeating stopped: {err}");
            }
            logger.Warn("HeartBeating stopped.");
        }

        private ISocket CreateHeartBeatSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            foreach (var uri in config.GetHeartBeatAddressRange())
            {
                try
                {
                    socket.Bind(uri);
                    config.SetActiveHeartBeatAddress(uri);

                    logger.Info($"{GetType().Name} started at Uri:{uri.ToSocketAddress()}");

                    return socket;
                }
                catch (NetMQException)
                {
                    logger.Info($"{GetType().Name} failed to bind to {uri.ToSocketAddress()}, retrying with next endpoint...");
                }
            }
            socket?.Dispose();

            throw new Exception($"{GetType().Name} failed to bind to any of the configured HeartBeat endpoints!");
        }
    }
}