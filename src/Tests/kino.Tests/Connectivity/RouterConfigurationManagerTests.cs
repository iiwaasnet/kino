using System;
using System.Threading.Tasks;
using kino.Core.Connectivity;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class RouterConfigurationManagerTests
    {
        private readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(5);
        private RouterConfiguration routerConfig;
        private ScaleOutSocketConfiguration scaleOutConfig;
        private SocketEndpoint scaleOutAddress;
        private RouterConfigurationManager routerConfigManager;

        [SetUp]
        public void Setup()
        {
            routerConfig = new RouterConfiguration {RouterAddress = new SocketEndpoint("tcp://*:3000")};
            scaleOutAddress = new SocketEndpoint("tcp://*:3001");
            scaleOutConfig = new ScaleOutSocketConfiguration
                             {
                                 AddressRange = new[]
                                                {
                                                    scaleOutAddress,
                                                    new SocketEndpoint("tcp://*:3002")
                                                }
                             };
            routerConfigManager = new RouterConfigurationManager(routerConfig, scaleOutConfig);
        }

        [Test]
        public void GetScaleOutAddress_BlocksIfSetActiveScaleOutAddressIsNotCalled()
        {
            var routerConfigManager = new RouterConfigurationManager(routerConfig, scaleOutConfig);
            //
            var task = Task.Factory.StartNew(() => routerConfigManager.GetScaleOutAddress());
            //
            Assert.IsFalse(task.Wait(CompletionTimeout));
        }

        [Test]
        public void GetScaleOutAddress_ReturnsSocketEndpointIfSetActiveScaleOutAddressIsCalled()
        {
            var task = Task<SocketEndpoint>.Factory.StartNew(() => routerConfigManager.GetScaleOutAddress());
            routerConfigManager.SetActiveScaleOutAddress(scaleOutAddress);
            //
            Assert.AreEqual(task.Result, scaleOutAddress);
        }

        [Test]
        public void SetActiveScaleOutAddress_ThrowsExceptionIfScaleOutAddressIsNotTheOneFromConfiguredRange()
        {
            var socketEndpoint = new SocketEndpoint("tcp://192.168.0.1:4000");

            CollectionAssert.DoesNotContain(scaleOutConfig.AddressRange, socketEndpoint);
            Assert.Throws<Exception>(() => routerConfigManager.SetActiveScaleOutAddress(socketEndpoint));
        }

        [Test]
        public void GetRouterConfiguration_BlocksIfSetMessageRouterConfigurationActiveIsNotCalled()
        {
            var task = Task.Factory.StartNew(() => routerConfigManager.GetRouterConfiguration());
            //
            Assert.IsFalse(task.Wait(CompletionTimeout));
        }

        [Test]
        public void GetInactiveRouterConfiguration_NeverBlocks()
        {
            var config = routerConfigManager.GetInactiveRouterConfiguration();
            //
            Assert.AreEqual(routerConfig.RouterAddress, config.RouterAddress);
        }

        [Test]
        public void GetRouterConfiguration_ReturnsRouterConfigurationIfSetMessageRouterConfigurationActiveIsCalled()
        {
            var task = Task<RouterConfiguration>.Factory.StartNew(() => routerConfigManager.GetRouterConfiguration());
            routerConfigManager.SetMessageRouterConfigurationActive();
            //
            Assert.AreEqual(task.Result.RouterAddress, routerConfig.RouterAddress);
        }
    }
}