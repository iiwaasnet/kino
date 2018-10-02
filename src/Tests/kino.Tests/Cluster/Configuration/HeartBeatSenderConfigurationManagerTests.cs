using System;
using System.Linq;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Core.Framework;
using kino.Tests.Helpers;
using NUnit.Framework;

namespace kino.Tests.Cluster.Configuration
{
    public class HeartBeatSenderConfigurationManagerTests
    {
        private HeartBeatSenderConfigurationManager configManager;
        private HeartBeatSenderConfiguration config;

        [SetUp]
        public void Setup()
        {
            config = new HeartBeatSenderConfiguration
                     {
                         AddressRange = Randomizer.Int32(3, 6)
                                                  .Produce(i => new Uri($"tcp://127.0.0.{i}:8080"))
                     };
            configManager = new HeartBeatSenderConfigurationManager(config);
        }

        [Test]
        public void GetHeartBeatAddressBlock_IfSetActiveHeartBeatAddressIsNotSet()
        {
            var task = Task.Factory.StartNew(() => configManager.GetHeartBeatAddress());
            //
            Assert.False(task.Wait(TimeSpan.FromSeconds(3)));
        }

        [Test]
        public void GetScaleOutAddressUnblocks_WhenActiveScaleOutAddressIsSet()
        {
            var asyncOp = TimeSpan.FromSeconds(4);
            var task = Task.Factory.StartNew(() => configManager.GetHeartBeatAddress());
            Task.Factory.StartNew(() =>
                                  {
                                      asyncOp.DivideBy(2).Sleep();
                                      configManager.SetActiveHeartBeatAddress(config.AddressRange.First().ToSocketAddress());
                                  });
            //
            Assert.True(task.Wait(asyncOp));
        }

        [Test]
        public void IfSocketEndpointDoesNotBelongToInitialAddressRange_SetActiveScaleOutAddressThrowsException()
        {
            Assert.Throws<Exception>(() => configManager.SetActiveHeartBeatAddress("inproc://test"));
        }
    }
}