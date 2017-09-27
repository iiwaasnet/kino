using System;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using Microsoft.Extensions.Logging;
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

        private void SendHeartBeat(CancellationToken token)
        {
            try
            {
                using (var socket = CreateHeartBeatSocket())
                {
                    var heartBeatMessage = new HeartBeatMessage
                                           {
                                               SocketIdentity = scaleOutConfigurationProvider.GetScaleOutAddress().Identity,
                                               HeartBeatInterval = config.GetHeartBeatInterval(),
                                               HealthUri = config.GetHeartBeatAddress().ToSocketAddress()
                                           };
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            socket.SendMessage(Message.Create(heartBeatMessage));
                            //logger.LogDebug($"HeartBeat sent at {DateTime.UtcNow} UTC");
                            config.GetHeartBeatInterval().Sleep(token);
                            //await Task.Delay(config.GetHeartBeatInterval(), token);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception err)
                        {
                            logger.LogError(err);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                logger.LogError(err);
            }
            logger.LogWarning("HeartBeating stopped.");
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

                    logger.LogInformation($"{GetType().Name} started at Uri:{uri.ToSocketAddress()}");

                    return socket;
                }
                catch (NetMQException)
                {
                    logger.LogInformation($"{GetType().Name} failed to bind to {uri.ToSocketAddress()}, retrying with next endpoint...");
                }
            }
            socket?.Dispose();

            throw new Exception($"{GetType().Name} failed to bind to any of the configured HeartBeat endpoints!");
        }
    }
}