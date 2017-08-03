using System;
using System.Linq;
using kino.Consensus.Configuration;
using kino.Core.Framework;
using kino.Tests.Helpers;
using NUnit.Framework;

namespace kino.Tests.Consensus
{
    [TestFixture]
    public class SynodConfigurationProviderTests
    {
        private SynodConfigurationProvider synodConfigProvider;
        private SynodConfiguration synodConfig;

        [SetUp]
        public void Setup()
        {
            synodConfig = new SynodConfiguration
                          {
                              LocalNode = "tcp://127.0.0.1:9090",
                              IntercomEndpoint = "inproc://health",
                              Members = 3.Produce(i => $"tcp://127.0.0.19{i}:9191")
                                         .ToList()
                          };
            synodConfigProvider = new SynodConfigurationProvider(synodConfig);
        }

        [Test]
        public void NodeWithSameUriAsOneOfSynodMembers_BelongsToSynod()
        {
            var node = new Uri(synodConfig.Members.Second()).BuildIpAddressUri();
            //
            Assert.IsTrue(synodConfigProvider.BelongsToSynod(node));
        }

        [Test]
        public void NodeWithDifferentUri_DoesntBelongToSynod()
        {
            var node = "tcp://127.0.0.2:8080".ParseAddress();
            //
            Assert.IsFalse(synodConfigProvider.BelongsToSynod(node));
        }
    }
}