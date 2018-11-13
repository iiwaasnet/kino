using System;
using System.Collections.Concurrent;
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
        private readonly ILocalSendingSocket<IMessage> localRouterSocket;
        private readonly IScaleOutConfigurationManager scaleOutConfigurationManager;
        private readonly ISecurityProvider securityProvider;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private BlockingCollection<Message> receiptConfirmationQueue;
        private readonly ILogger logger;
        private Task listening;
        private Task receiptConfirmation;
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
            receiptConfirmationQueue = new BlockingCollection<Message>(new ConcurrentQueue<Message>());
            this.logger = logger;
        }

        public void Start()
        {
            receiptConfirmationQueue = new BlockingCollection<Message>(new ConcurrentQueue<Message>());
            cancellationTokenSource = new CancellationTokenSource();
            using (var barrier = new Barrier(3))
            {
                listening = Task.Factory
                                .StartNew(_ => RoutePeerMessages(cancellationTokenSource.Token, barrier), TaskCreationOptions.LongRunning)
                                .ContinueWith(task =>
                                              {
                                                  logger.Error(task.Exception);
                                                  cancellationTokenSource.Cancel();
                                              },
                                              TaskContinuationOptions.OnlyOnFaulted);
                receiptConfirmation = Task.Factory
                                          .StartNew(_ => SendReceiptConfirmations(cancellationTokenSource.Token, barrier), TaskCreationOptions.LongRunning)
                                          .ContinueWith(task =>
                                                        {
                                                            logger.Error(task.Exception);
                                                            cancellationTokenSource.Cancel();
                                                        },
                                                        TaskContinuationOptions.OnlyOnFaulted);
                barrier.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            listening?.Wait();
            receiptConfirmation?.Wait();
            cancellationTokenSource?.Dispose();
            receiptConfirmationQueue?.Dispose();
        }

        private void RoutePeerMessages(CancellationToken token, Barrier barrier)
        {
            try
            {
                using (var scaleOutFrontend = CreateScaleOutFrontendSocket())
                {
                    barrier.SignalAndWait(token);

                    while (!token.IsCancellationRequested)
                    {
                        Message message = null;
                        try
                        {
                            message = scaleOutFrontend.ReceiveMessage(token).As<Message>();
                            if (message != null)
                            {
                                message.VerifySignature(securityProvider);
                                var receiptConfirmationRequested = message.RemoveCallbackPoint(KinoMessages.ReceiptConfirmation);

                                if (receiptConfirmationRequested)
                                {
                                    receiptConfirmationQueue.Add(message, token);
                                }

                                localRouterSocket.Send(message);

                                ReceivedFromOtherNode(message);
                            }
                        }
                        //TODO: Check why sending exception message was only in case of SecurityException
                        //catch (SecurityException err)
                        //{
                        //    CallbackException(err, message);
                        //    logger.Error(err);
                        //}
                        catch (Exception err)
                        {
                            CallbackException(err, message);
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

        private void SendReceiptConfirmations(CancellationToken token, Barrier barrier)
        {
            try
            {
                barrier.SignalAndWait(token);

                foreach (var message in receiptConfirmationQueue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        SendReceiptConfirmation(message);
                    }
                    catch (Exception err)
                    {
                        logger.Error(err);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                logger.Error(err);
            }

            logger.Warn($"{nameof(SendReceiptConfirmations)} thread stopped!");
        }

        private ISocket CreateScaleOutFrontendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            foreach (var scaleOutAddress in scaleOutConfigurationManager.GetScaleOutAddressRange())
            {
                try
                {
                    socket.SetIdentity(scaleOutAddress.Identity);
                    socket.SetMandatoryRouting();
                    socket.SetReceiveHighWaterMark(GetScaleOutReceiveMessageQueueLength());
                    socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterScaleoutFrontendSocketReceiveRate);

                    socket.Bind(scaleOutAddress.Uri);
                    scaleOutConfigurationManager.SetActiveScaleOutAddress(scaleOutAddress);

                    logger.Info($"MessageRouter started at Uri:{scaleOutAddress.Uri} " +
                                $"Identity:{scaleOutAddress.Identity.GetAnyString()}");

                    return socket;
                }
                catch
                {
                    logger.Info($"Failed to bind to {scaleOutAddress.Uri}, retrying with next endpoint...");
                }
            }

            throw new Exception("Failed to bind to any of the configured ScaleOut endpoints!");
        }

        private int GetScaleOutReceiveMessageQueueLength()
        {
            var hwm = scaleOutConfigurationManager.GetScaleOutReceiveMessageQueueLength();
            var internalSocketsHWM = socketFactory.GetSocketConfiguration().ReceivingHighWatermark;

            if (hwm == 0 || hwm > internalSocketsHWM)
            {
                logger.Warn($"ScaleOutReceiveMessageQueueLength ({hwm}) cannot be greater, than internal ReceivingHighWatermark ({internalSocketsHWM}). " +
                            $"Current value of ScaleOutReceiveMessageQueueLength will be set to {internalSocketsHWM}.");
                hwm = internalSocketsHWM;
            }

            return hwm;
        }

        private void SendReceiptConfirmation(Message messageIn)
        {
            var messageOut = Message.Create(ReceiptConfirmationMessage.Instance)
                                    .As<Message>();
            messageOut.SetDomain(securityProvider.GetDomain(KinoMessages.ReceiptConfirmation.Identity));

            messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverNodeIdentity,
                                             messageIn.CallbackReceiverIdentity,
                                             KinoMessages.ReceiptConfirmation,
                                             messageIn.CallbackKey);
            messageOut.SetCorrelationId(messageIn.CorrelationId);
            messageOut.CopyMessageRouting(messageIn.GetMessageRouting());
            messageOut.TraceOptions |= messageIn.TraceOptions;

            localRouterSocket.Send(messageOut);
        }

        private void CallbackException(Exception err, Message messageIn)
        {
            var messageOut = Message.Create(new ExceptionMessage
                                            {
                                                Exception = err,
                                                StackTrace = err.StackTrace
                                            })
                                    .As<Message>();
            messageOut.SetDomain(securityProvider.GetDomain(KinoMessages.Exception.Identity));

            if (messageIn != null)
            {
                messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverNodeIdentity,
                                                 messageIn.CallbackReceiverIdentity,
                                                 messageIn.CallbackPoint,
                                                 messageIn.CallbackKey);
                messageOut.SetCorrelationId(messageIn.CorrelationId);
                messageOut.CopyMessageRouting(messageIn.GetMessageRouting());
                messageOut.TraceOptions |= messageIn.TraceOptions;
            }

            localRouterSocket.Send(messageOut);
        }
    }
}