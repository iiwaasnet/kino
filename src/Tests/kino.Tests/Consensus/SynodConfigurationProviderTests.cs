using System;
using System.Linq;
using kino.Consensus.Configuration;
using kino.Core.Framework;
using kino.Tests.Helpers;
using Xunit;

namespace kino.Tests.Consensus
{
    public class SynodConfigurationProviderTests
    {
        private readonly SynodConfigurationProvider synodConfigProvider;
        private readonly SynodConfiguration synodConfig;

        public SynodConfigurationProviderTests()
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

        [Fact]
        public void NodeWithSameUriAsOneOfSynodMembers_BelongsToSynod()
        {
            var node = new Uri(synodConfig.Members.Second()).BuildIpAddressUri();
            //
            Assert.True(synodConfigProvider.BelongsToSynod(node));
        }

        [Fact]
        public void NodeWithDifferentUri_DoesntBelongToSynod()
        {
            var node = "tcp://127.0.0.2:8080".ParseAddress();
            //
            Assert.False(synodConfigProvider.BelongsToSynod(node));
        }
    }
}