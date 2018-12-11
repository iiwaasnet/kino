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
using NUnit.Framework;

namespace kino.Tests.Cluster
{
    public class HeartBeatSenderTests
    {
        private HeartBeatSender heartBeatSender;
        private Mock<ISocketFactory> socketFactory;
        private Mock<IScaleOutConfigurationProvider> scaleOutConfigurationProvider;
        private Mock<ILogger> logger;
        private Mock<IHeartBeatSenderConfigurationManager> config;
        private SocketEndpoint scaleOutAddress;
        private TimeSpan heartBeatInterval;
        private IEnumerable<string> heartBeatAddresses;
        private Mock<ISocket> socket;

        [SetUp]
        public void Setup()
        {
            socketFactory = new Mock<ISocketFactory>();
            socket = new Mock<ISocket>();
            socketFactory.Setup(m => m.CreatePublisherSocket()).Returns(socket.Object);
            config = new Mock<IHeartBeatSenderConfigurationManager>();
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            scaleOutAddress = SocketEndpoint.Parse("tcp://127.0.0.1:8080", Guid.NewGuid().ToByteArray());
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            heartBeatInterval = TimeSpan.FromMilliseconds(800);
            config.Setup(m => m.GetHeartBeatInterval()).Returns(heartBeatInterval);
            heartBeatAddresses = new[] {"tcp://127.0.0.1:9090", "tcp://127.0.0.2:9090"};
            config.Setup(m => m.GetHeartBeatAddressRange()).Returns(heartBeatAddresses);
            config.Setup(m => m.GetHeartBeatAddress()).Returns(heartBeatAddresses.First());
            logger = new Mock<ILogger>();
            heartBeatSender = new HeartBeatSender(socketFactory.Object,
                                                  config.Object,
                                                  scaleOutConfigurationProvider.Object,
                                                  logger.Object);
        }

        [Test]
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
                                                          Assert.AreEqual(heartBeatInterval, payload.HeartBeatInterval);
                                                          return true;
                                                      };
            socket.Verify(m => m.Send(It.Is<IMessage>(msg => isHeartBeatMessage(msg))), Times.AtLeast(heartBeatsToSend));
        }

        [Test]
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
            socket.Verify(m => m.Send(It.IsAny<IMessage>()), Times.AtLeastOnce);
            socket.Verify(m => m.Dispose(), Times.Once);
        }

        [Test]
        public void IfSocketFailsBindingToAllAddress_HeartBeatSenderDoesntSendMessages()
        {
            var asyncOp = TimeSpan.FromSeconds(1);
            socket.Setup(m => m.Bind(It.IsAny<string>())).Throws<NetMQException>();
            //
            heartBeatSender.Start();
            asyncOp.Sleep();
            heartBeatSender.Stop();
            //
            socket.Verify(m => m.Bind(It.IsAny<string>()), Times.Exactly(heartBeatAddresses.Count()));
            socket.Verify(m => m.Dispose(), Times.Once);
            config.Verify(m => m.SetActiveHeartBeatAddress(It.IsAny<string>()), Times.Never);
            socket.Verify(m => m.Send(It.IsAny<IMessage>()), Times.Never);
            logger.Verify(m => m.Error(It.IsAny<Exception>()), Times.Once);
            logger.Verify(m => m.Warn(It.Is<object>(f => f.ToString() == "HeartBeating stopped.")), Times.Once);
        }
    }
}