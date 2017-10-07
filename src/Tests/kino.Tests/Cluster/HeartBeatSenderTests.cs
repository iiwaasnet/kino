using System;
using System.Collections.Generic;
using System.Linq;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using Moq;
using NetMQ;
using Xunit;

namespace kino.Tests.Cluster
{
    public class HeartBeatSenderTests
    {
        private readonly HeartBeatSender heartBeatSender;
        private readonly Mock<ISocketFactory> socketFactory;
        private readonly Mock<IScaleOutConfigurationProvider> scaleOutConfigurationProvider;
        private readonly Mock<ILogger> logger;
        private readonly Mock<IHeartBeatSenderConfigurationManager> config;
        private readonly SocketEndpoint scaleOutAddress;
        private TimeSpan heartBeatInterval;
        private readonly IEnumerable<Uri> heartBeatAddresses;
        private readonly Mock<ISocket> socket;

        public HeartBeatSenderTests()
        {
            socketFactory = new Mock<ISocketFactory>();
            socket = new Mock<ISocket>();
            socketFactory.Setup(m => m.CreatePublisherSocket()).Returns(socket.Object);
            config = new Mock<IHeartBeatSenderConfigurationManager>();
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            scaleOutAddress = new SocketEndpoint("tcp://127.0.0.1:8080");
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            heartBeatInterval = TimeSpan.FromMilliseconds(800);
            config.Setup(m => m.GetHeartBeatInterval()).Returns(heartBeatInterval);
            heartBeatAddresses = new[] {new Uri("tcp://127.0.0.1:9090"), new Uri("tcp://127.0.0.2:9090")};
            config.Setup(m => m.GetHeartBeatAddressRange()).Returns(heartBeatAddresses);
            config.Setup(m => m.GetHeartBeatAddress()).Returns(heartBeatAddresses.First());
            logger = new Mock<ILogger>();
            heartBeatSender = new HeartBeatSender(socketFactory.Object,
                                                  config.Object,
                                                  scaleOutConfigurationProvider.Object,
                                                  logger.Object);
        }

        [Fact]
        public void HeartBeatMessageIsSent_EveryHeartBeatInterval()
        {
            var heartBeatsToSend = 2;
            var asyncOp = heartBeatInterval.MultiplyBy(heartBeatsToSend + 1);
            //
            heartBeatSender.Start();
            asyncOp.Sleep();
            heartBeatSender.Stop();
            //
            Func<IMessage, bool> isHeartBeatMessage = msg =>
                                                      {
                                                          var payload = msg.GetPayload<HeartBeatMessage>();
                                                          Assert.True(Unsafe.ArraysEqual(scaleOutAddress.Identity, payload.SocketIdentity));
                                                          Assert.Equal(heartBeatInterval, payload.HeartBeatInterval);
                                                          return true;
                                                      };
            socket.Verify(m => m.SendMessage(It.Is<IMessage>(msg => isHeartBeatMessage(msg))), Times.AtLeast(heartBeatsToSend));
        }

        [Fact]
        public void IfSocketFailsBindingToOneAddress_ItRetriesWithTheNextOne()
        {
            var asyncOp = TimeSpan.FromSeconds(1);
            socket.Setup(m => m.Bind(heartBeatAddresses.First())).Throws<NetMQException>();
            //
            heartBeatSender.Start();
            asyncOp.Sleep();
            heartBeatSender.Stop();
            //
            var activeAddress = heartBeatAddresses.Second();
            socket.Verify(m => m.Bind(activeAddress), Times.Once());
            config.Verify(m => m.SetActiveHeartBeatAddress(activeAddress), Times.Once);
            socket.Verify(m => m.SendMessage(It.IsAny<IMessage>()), Times.AtLeastOnce);
            socket.Verify(m => m.Dispose(), Times.Once);
        }

        [Fact]
        public void IfSocketFailsBindingToAllAddress_HeartBeatSenderDoesntSendMessages()
        {
            var asyncOp = TimeSpan.FromSeconds(1);
            socket.Setup(m => m.Bind(It.IsAny<Uri>())).Throws<NetMQException>();
            //
            heartBeatSender.Start();
            asyncOp.Sleep();
            heartBeatSender.Stop();
            //
            socket.Verify(m => m.Bind(It.IsAny<Uri>()), Times.Exactly(heartBeatAddresses.Count()));
            socket.Verify(m => m.Dispose(), Times.Once);
            config.Verify(m => m.SetActiveHeartBeatAddress(It.IsAny<Uri>()), Times.Never);
            socket.Verify(m => m.SendMessage(It.IsAny<IMessage>()), Times.Never);
            logger.Verify(m => m.Error(It.IsAny<Exception>()), Times.Once);
            logger.Verify(m => m.Warn(It.Is<object>(f => f.ToString() == "HeartBeating stopped.")), Times.Once);
        }
    }
}