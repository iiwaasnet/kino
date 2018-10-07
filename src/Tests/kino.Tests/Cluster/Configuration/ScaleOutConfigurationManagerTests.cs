using System;
using System.Linq;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Framework;
using kino.Tests.Helpers;
using NUnit.Framework;

namespace kino.Tests.Cluster.Configuration
{
    public class ScaleOutConfigurationManagerTests
    {
        private ScaleOutConfigurationManager configManager;
        private ScaleOutSocketConfiguration config;

        [SetUp]
        public void Setup()
        {
            config = new ScaleOutSocketConfiguration
                     {
                         AddressRange = Randomizer.Int32(3, 6)
                                                  .Produce(i => SocketEndpoint.Parse($"tcp://*:808{i}", Guid.NewGuid().ToByteArray()))
                     };
            configManager = new ScaleOutConfigurationManager(config);
        }

        [Test]
        public void IfActiveScaleOutAddressIsNotSet_GetScaleOutAddressBlocks()
        {
            var task = Task.Factory.StartNew(() => configManager.GetScaleOutAddress());
            //
            Assert.False(task.Wait(TimeSpan.FromSeconds(3)));
        }

        [Test]
        public void GetScaleOutAddressUnblocks_WhenActiveScaleOutAddressIsSet()
        {
            var asyncOp = TimeSpan.FromSeconds(4);
            var task = Task.Factory.StartNew(() => configManager.GetScaleOutAddress());
            Task.Factory.StartNew(() =>
                                  {
                                      asyncOp.DivideBy(2).Sleep();
                                      configManager.SetActiveScaleOutAddress(config.AddressRange.First());
                                  });
            //
            Assert.True(task.Wait(asyncOp));
        }

        [Test]
        public void IfSocketEndpointDoesNotBelongToInitialAddressRange_SetActiveScaleOutAddressThrowsException()
        {
            Assert.Throws<Exception>(() => configManager.SetActiveScaleOutAddress(SocketEndpoint.Parse("tcp://*:43", Guid.NewGuid().ToByteArray())));
        }
    }
}