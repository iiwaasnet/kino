using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core;
using kino.Core.Framework;

namespace kino.Consensus.Configuration
{
    public class SynodConfigurationProvider : ISynodConfigurationProvider
    {
        private volatile IEnumerable<DynamicUri> synod;

        public SynodConfigurationProvider(SynodConfiguration config)
        {
            LocalNode = new Node(config.LocalNode.ParseAddress(), ReceiverIdentifier.CreateIdentity());
            synod = config.Members
                          .Select(uri => new DynamicUri(uri))
                          .ToList();
            HeartBeatInterval = config.HeartBeatInterval;
            MissingHeartBeatsBeforeReconnect = config.MissingHeartBeatsBeforeReconnect;
            IntercomEndpoint = new Uri(config.IntercomEndpoint).ToSocketAddress();
        }

        public bool BelongsToSynod(string node)
            => synod.Any(n => n.Uri == node);

        public Node LocalNode { get; }

        public IEnumerable<DynamicUri> Synod => synod;

        public TimeSpan HeartBeatInterval { get; }

        public int MissingHeartBeatsBeforeReconnect { get; }

        public string IntercomEndpoint { get; }
    }
}