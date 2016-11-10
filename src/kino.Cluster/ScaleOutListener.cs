using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Cluster
{
    public partial class ScaleOutListener : IScaleOutListener
    {
        private readonly ISocketFactory socketFactory;
        private readonly ILocalSendingSocket<Message> localRouterSocket;
        private readonly IScaleOutConfigurationManager scaleOutConfigurationManager;
        private readonly ISecurityProvider securityProvider;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ILogger logger;
        private Task listening;
        private CancellationTokenSource cancellationTokenSource;

        public ScaleOutListener(ISocketFactory socketFactory,
                                ILocalSendingSocket<IMessage> localRouterSocket,
                                IScaleOutConfigurationManager scaleOutConfigurationManager,
                                ISecurityProvider securityProvider,
                                IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.localRouterSocket = localRouterSocket;
            this.scaleOutConfigurationManager = scaleOutConfigurationManager;
            this.securityProvider = securityProvider;
            this.performanceCounterManager = performanceCounterManager;
            this.logger = logger;
        }

        public void Start()
        {
            cancellationTokenSource = new CancellationTokenSource();
            listening = Task.Factory.StartNew(_ => RoutePeerMessages(cancellationTokenSource.Token),
                                              TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            listening?.Wait();
            cancellationTokenSource?.Dispose();
        }

        private void RoutePeerMessages(CancellationToken token)
        {
            try
            {
                using (var scaleOutFrontend = CreateScaleOutFrontendSocket())
                {
                    while (!token.IsCancellationRequested)
                    {
                        Message message = null;
                        try
                        {
                            message = (Message) scaleOutFrontend.ReceiveMessage(token);
                            if (message != null)
                            {
                                message.VerifySignature(securityProvider);
                                localRouterSocket.Send(message);

                                ReceivedFromOtherNode(message);
                            }
                        }
                        catch (SecurityException err)
                        {
                            CallbackSecurityException(err, message);
                            logger.Error(err);
                        }
                        catch (Exception err)
                        {
                            logger.Error(err);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private ISocket CreateScaleOutFrontendSocket()
        {
            var routerConfiguration = scaleOutConfigurationManager.GetRouterConfiguration();

            var socket = socketFactory.CreateRouterSocket();
            foreach (var scaleOutAddress in scaleOutConfigurationManager.GetScaleOutAddressRange())
            {
                try
                {
                    socket.SetIdentity(scaleOutAddress.Identity);
                    socket.SetMandatoryRouting();
                    socket.SetReceiveHighWaterMark(GetScaleOutReceiveMessageQueueLength(routerConfiguration));
                    socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterScaleoutFrontendSocketReceiveRate);

                    socket.Bind(scaleOutAddress.Uri);
                    scaleOutConfigurationManager.SetActiveScaleOutAddress(scaleOutAddress);

                    logger.Info($"MessageRouter started at Uri:{scaleOutAddress.Uri.ToSocketAddress()} " +
                                $"Identity:{scaleOutAddress.Identity.GetAnyString()}");

                    return socket;
                }
                catch
                {
                    logger.Info($"Failed to bind to {scaleOutAddress.Uri.ToSocketAddress()}, retrying with next endpoint...");
                }
            }

            throw new Exception("Failed to bind to any of the configured ScaleOut endpoints!");
        }

        private int GetScaleOutReceiveMessageQueueLength(RouterConfiguration config)
        {
            var hwm = config.ScaleOutReceiveMessageQueueLength;
            var internalSocketsHWM = socketFactory.GetSocketDefaultConfiguration().ReceivingHighWatermark;

            if (hwm == 0 || hwm > internalSocketsHWM)
            {
                logger.Warn($"ScaleOutReceiveMessageQueueLength ({hwm}) cannot be greater, than internal ReceivingHighWatermark ({internalSocketsHWM}). " +
                            $"Current value of ScaleOutReceiveMessageQueueLength will be set to {internalSocketsHWM}.");
                hwm = internalSocketsHWM;
            }

            return hwm;
        }

        private void CallbackSecurityException(Exception err, Message messageIn)
        {
            var messageOut = Message.Create(new ExceptionMessage
                                            {
                                                Exception = err,
                                                StackTrace = err.StackTrace
                                            },
                                            securityProvider.GetDomain(KinoMessages.Exception.Identity))
                                    .As<Message>();
            messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverIdentity, messageIn.CallbackPoint, messageIn.CallbackKey);
            messageOut.SetCorrelationId(messageIn.CorrelationId);
            messageOut.CopyMessageRouting(messageIn.GetMessageRouting());
            messageOut.TraceOptions |= messageIn.TraceOptions;

            localRouterSocket.Send(messageOut);
        }
    }
}