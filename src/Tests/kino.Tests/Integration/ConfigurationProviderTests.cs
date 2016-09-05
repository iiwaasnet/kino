using System.Linq;
using Autofac.kino;
using NUnit.Framework;

namespace kino.Tests.Integration
{
    [TestFixture]
    public class ConfigurationProviderTests
    {
        [Test]
        [TestCase("tcp://127.0.0.1:3000-3002", 3000, 3001, 3002)]
        [TestCase("tcp://127.0.0.1:3000-3000", 3000)]
        [TestCase("tcp://127.0.0.1:3000", 3000)]
        [TestCase("tcp://127.0.0.1:3000-3001", 3000, 3001)]
        public void GetScaleOutConfiguration_ReturnsSocketEndpointsForCompletePortRange(string scaleOutAddress, params int[] portNumbers)
        {
            var config = new ApplicationConfiguration
                         {
                             ScaleOutAddressUri = scaleOutAddress
                         };
            var configProvider = new ConfigurationProvider(config);
            var scaleOutConfig = configProvider.GetScaleOutConfiguration();

            for (var i = 0; i < portNumbers.Length; i++)
            {
                Assert.AreEqual(portNumbers[i], scaleOutConfig.AddressRange.ElementAt(i).Uri.Port);
            }
        }
    }
}