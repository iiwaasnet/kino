using System;
using System.Linq;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Core.Framework;
using kino.Tests.Helpers;
using Xunit;

namespace kino.Tests.Cluster.Configuration
{
    public class HeartBeatSenderConfigurationManagerTests
    {
        private readonly HeartBeatSenderConfigurationManager configManager;
        private readonly HeartBeatSenderConfiguration config;

        public HeartBeatSenderConfigurationManagerTests()
        {
            config = new HeartBeatSenderConfiguration
                     {
                         AddressRange = Randomizer.Int32(3, 6)
                                                  .Produce(i => new Uri($"tcp://127.0.0.{i}:8080"))
                     };
            configManager = new HeartBeatSenderConfigurationManager(config);
        }

        [Fact]
        public void GetHeartBeatAddressBlock_IfSetActiveHeartBeatAddressIsNotSet()
        {
            var task = Task.Factory.StartNew(() => configManager.GetHeartBeatAddress());
            //
            Assert.False(task.Wait(TimeSpan.FromSeconds(3)));
        }

        [Fact]
        public void GetScaleOutAddressUnblocks_WhenActiveScaleOutAddressIsSet()
        {
            var asyncOp = TimeSpan.FromSeconds(4);
            var task = Task.Factory.StartNew(() => configManager.GetHeartBeatAddress());
            Task.Factory.StartNew(() =>
                                  {
                                      asyncOp.DivideBy(2).Sleep();
                                      configManager.SetActiveHeartBeatAddress(config.AddressRange.First());
                                  });
            //
            Assert.True(task.Wait(asyncOp));
        }

        [Fact]
        public void IfSocketEndpointDoesntBelongToInitialAddressRange_SetActiveScaleOutAddressThrowsException()
        {
            Assert.Throws<Exception>(() => configManager.SetActiveHeartBeatAddress(new Uri("inproc://test")));
        }
    }
}