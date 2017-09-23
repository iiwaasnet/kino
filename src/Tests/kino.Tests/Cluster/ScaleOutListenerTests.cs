using System;
using System.Linq;
using System.Threading;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using Xunit;
using CollectionAssert = FluentAssertions.AssertionExtensions;

namespace kino.Tests.Cluster
{
    public class ScaleOutListenerTests
    {
        private readonly ScaleOutListener scaleOutListener;
        private readonly Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private readonly Mock<ILogger> logger;
        private readonly Mock<ISocketFactory> socketFactory;
        private readonly Mock<ILocalSendingSocket<IMessage>> localRouterSocket;
        private readonly Mock<IScaleOutConfigurationManager> scaleOutConfigurationManager;
        private readonly Mock<ISecurityProvider> securityProvider;
        private readonly Mock<ISocket> frontEndSocket;
        private readonly SocketEndpoint[] scaleOutAddresses;
        private readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(500);
        private readonly SocketConfiguration socketConfig;
        private readonly CancellationTokenSource tokenSource;

        public ScaleOutListenerTests()
        {
            tokenSource = new CancellationTokenSource();
            frontEndSocket = new Mock<ISocket>();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateRouterSocket()).Returns(frontEndSocket.Object);
            socketConfig = new SocketConfiguration();
            socketFactory.Setup(m => m.GetSocketConfiguration()).Returns(socketConfig);
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            var perfCounter = new Mock<IPerformanceCounter>();
            performanceCounterManager.Setup(m => m.GetCounter(It.IsAny<KinoPerformanceCounters>())).Returns(perfCounter.Object);
            logger = new Mock<ILogger>();
            localRouterSocket = new Mock<ILocalSendingSocket<IMessage>>();
            scaleOutConfigurationManager = new Mock<IScaleOutConfigurationManager>();
            scaleOutAddresses = new[]
                                {
                                    new SocketEndpoint("tcp://127.0.0.1:8080"),
                                    new SocketEndpoint("tcp://127.0.0.2:9090")
                                };
            scaleOutConfigurationManager.Setup(m => m.GetScaleOutAddressRange()).Returns(scaleOutAddresses);
            scaleOutConfigurationManager.Setup(m => m.GetScaleOutReceiveMessageQueueLength()).Returns(1000);
            securityProvider = new Mock<ISecurityProvider>();
            scaleOutListener = new ScaleOutListener(socketFactory.Object,
                                                    localRouterSocket.Object,
                                                    scaleOutConfigurationManager.Object,
                                                    securityProvider.Object,
                                                    performanceCounterManager.Object,
                                                    logger.Object);
        }

        [Fact]
        public void IfFrontEndSocketReturnsNull_LocalRouterSocketSendIsNotCalled()
        {
            frontEndSocket.SetupMessageReceived(null, tokenSource.Token);
            //
            scaleOutListener.Start();
            AsyncOp.Sleep();
            tokenSource.Cancel();
            scaleOutListener.Stop();
            //
            frontEndSocket.Verify(m => m.ReceiveMessage(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            localRouterSocket.Verify(m => m.Send(It.IsAny<IMessage>()), Times.Never);
        }

        [Fact]
        public void IfFrontEndSocketReturnsMessage_ItIsForwardedToLocalRouterSocket()
        {
            var message = Message.Create(new SimpleMessage());
            frontEndSocket.SetupMessageReceived(message, tokenSource.Token);
            //
            scaleOutListener.Start();
            AsyncOp.Sleep();
            tokenSource.Cancel();
            scaleOutListener.Stop();
            //
            localRouterSocket.Verify(m => m.Send(message), Times.Once);
        }

        [Fact]
        public void IfExceptionIsThrownAfterFrontEndSocketReceivedMessage_ExceptionMessageIsForwardedToLocalRouterSocketWithCallbackOfOriginalMessage()
        {
            var message = Message.Create(new SimpleMessage()).As<Message>();
            message.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                          Guid.NewGuid().ToByteArray(),
                                          new[] {KinoMessages.Ping, KinoMessages.Pong},
                                          Randomizer.Int64());
            message.PushRouterAddress(new SocketEndpoint("tcp://127.0.0.4:7878"));
            message.PushRouterAddress(new SocketEndpoint("tcp://127.0.0.5:5464"));
            message.SetCorrelationId(Guid.NewGuid().ToByteArray());
            frontEndSocket.SetupMessageReceived(message, tokenSource.Token);
            localRouterSocket.Setup(m => m.Send(message)).Throws<Exception>();
            //
            scaleOutListener.Start();
            AsyncOp.Sleep();
            tokenSource.Cancel();
            scaleOutListener.Stop();
            //
            Func<IMessage, bool> isExceptionOrInitalMessage = msg =>
                                                              {
                                                                  if (message.Equals(msg))
                                                                  {
                                                                      return true;
                                                                  }
                                                                  var exception = msg.As<Message>();
                                                                  Assert.Equal(KinoMessages.Exception, exception);
                                                                  Assert.True(Unsafe.ArraysEqual(message.CallbackReceiverNodeIdentity, exception.CallbackReceiverNodeIdentity));
                                                                  Assert.True(Unsafe.ArraysEqual(message.CallbackReceiverIdentity, exception.CallbackReceiverIdentity));
                                                                  CollectionAssert.Should(message.CallbackPoint).BeEquivalentTo(exception.CallbackPoint);
                                                                  CollectionAssert.Should(message.GetMessageRouting()).BeEquivalentTo(exception.GetMessageRouting());
                                                                  Assert.Equal(message.CallbackKey, exception.CallbackKey);
                                                                  Assert.True(Unsafe.ArraysEqual(message.CorrelationId, exception.CorrelationId));
                                                                  return true;
                                                              };
            localRouterSocket.Verify(m => m.Send(It.Is<IMessage>(msg => isExceptionOrInitalMessage(msg))), Times.Exactly(2));
        }

        [Fact]
        public void IfExceptionIsThrownFromFrontEndSocketReceiveMessageMethod_ExceptionMessageIsForwardedToLocalRouterSocket()
        {
            var message = Message.Create(new SimpleMessage()).As<Message>();
            message.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                          Guid.NewGuid().ToByteArray(),
                                          new[] {KinoMessages.Ping, KinoMessages.Pong},
                                          Randomizer.Int64());
            message.PushRouterAddress(new SocketEndpoint("tcp://127.0.0.4:7878"));
            message.PushRouterAddress(new SocketEndpoint("tcp://127.0.0.5:5464"));
            message.SetCorrelationId(Guid.NewGuid().ToByteArray());
            frontEndSocket.Setup(m => m.ReceiveMessage(It.IsAny<CancellationToken>())).Throws<Exception>();
            //
            scaleOutListener.Start();
            AsyncOp.Sleep();
            scaleOutListener.Stop();
            //
            Func<IMessage, bool> isExceptionMessage = msg =>
                                                      {
                                                          var exception = msg.As<Message>();
                                                          Assert.Equal(KinoMessages.Exception, exception);
                                                          Assert.Null(exception.CallbackReceiverNodeIdentity);
                                                          Assert.Null(exception.CallbackReceiverIdentity);
                                                          Assert.Empty(exception.CallbackPoint);
                                                          Assert.Empty(exception.GetMessageRouting());
                                                          Assert.Equal(0, exception.CallbackKey);
                                                          Assert.True(Unsafe.ArraysEqual(Guid.Empty.ToString().GetBytes(), exception.CorrelationId));
                                                          return true;
                                                      };
            localRouterSocket.Verify(m => m.Send(It.Is<IMessage>(msg => isExceptionMessage(msg))), Times.AtLeastOnce);
        }

        [Fact]
        public void IfFrontEndSocketFailsBindingToOneAddress_ItRetriesWithOtherAddresses()
        {
            frontEndSocket.Setup(m => m.Bind(scaleOutAddresses.First().Uri)).Throws<Exception>();
            //
            scaleOutListener.Start();
            scaleOutListener.Stop();
            //
            frontEndSocket.Verify(m => m.Bind(scaleOutAddresses.First().Uri), Times.Once);
            frontEndSocket.Verify(m => m.Bind(scaleOutAddresses.Second().Uri), Times.Once);
            logger.Verify(m => m.Info(It.IsAny<object>()), Times.Exactly(2));
        }

        [Theory]
        [InlineData(0, 2000)]
        [InlineData(3000, 2000)]
        public void IfFrontEndSocketHWMEqualsZeroOrGreaterThanDefaultHWM_ThenFrontEndSocketHWMIsSetToDefaultValue(int frontEndSocketHwm, int defaultHwm)
        {
            socketConfig.ReceivingHighWatermark = defaultHwm;
            scaleOutConfigurationManager.Setup(m => m.GetScaleOutReceiveMessageQueueLength()).Returns(frontEndSocketHwm);
            //
            scaleOutListener.Start();
            scaleOutListener.Stop();
            //
            frontEndSocket.Verify(m => m.SetReceiveHighWaterMark(defaultHwm), Times.Once);
            frontEndSocket.Verify(m => m.SetReceiveHighWaterMark(frontEndSocketHwm), Times.Never);
        }

        [Fact]
        public void IfFrontEndSocketHWMLessThanDefaultHWMAndGreaterThanZero_ThenFrontEndSocketHWMIsSet()
        {
            var defaultHwm = 2000;
            var frontEndSocketHwm = defaultHwm - 100;
            socketConfig.ReceivingHighWatermark = defaultHwm;
            scaleOutConfigurationManager.Setup(m => m.GetScaleOutReceiveMessageQueueLength()).Returns(frontEndSocketHwm);
            //
            scaleOutListener.Start();
            scaleOutListener.Stop();
            //
            frontEndSocket.Verify(m => m.SetReceiveHighWaterMark(frontEndSocketHwm), Times.Once);
            frontEndSocket.Verify(m => m.SetReceiveHighWaterMark(defaultHwm), Times.Never);
        }
    }
}