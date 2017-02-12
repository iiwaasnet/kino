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
    [TestFixture]
    public class ScaleOutConfigurationManagerTests
    {
        private ScaleOutConfigurationManager configManager;
        private ScaleOutSocketConfiguration config;

        [SetUp]
        public void Setup()
        {
            config = new ScaleOutSocketConfiguration
                     {
                         AddressRange = EnumerableExtenions.Produce(Randomizer.Int32(3, 6),
                                                                    i => new SocketEndpoint($"tcp://127.0.0.{i}:8080"))
                     };
            configManager = new ScaleOutConfigurationManager(config);
        }

        [Test]
        public void IfActiveScaleOutAddressIsNotSet_GetScaleOutAddressBlocks()
        {
            var task = Task.Factory.StartNew(() => configManager.GetScaleOutAddress());
            //
            Assert.IsFalse(task.Wait(TimeSpan.FromSeconds(3)));
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
            Assert.IsTrue(task.Wait(asyncOp));
        }

        [Test]
        public void IfSocketEndpointDoesntBelongToInitialAddressRange_SetActiveScaleOutAddressThrowsException()
        {
            Assert.Throws<Exception>(() => configManager.SetActiveScaleOutAddress(new SocketEndpoint("inproc://test")));
        }
    }
}