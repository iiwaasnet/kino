using System;
using System.Collections.Generic;
using kino.Core;

namespace kino.Consensus.Configuration
{
    public class SynodConfiguration : ISynodConfiguration
    {
        private readonly HashSet<Uri> synod;

        public SynodConfiguration(ISynodConfigurationProvider configProvider)
        {
            LocalNode = new Node(configProvider.LocalNode, ReceiverIdentifier.CreateIdentity());
            synod = new HashSet<Uri>(configProvider.Synod);
            HeartBeatInterval = configProvider.HeartBeatInterval;
            MissingHeartBeatsBeforeReconnect = configProvider.MissingHeartBeatsBeforeReconnect;
            IntercomEndpoint = configProvider.IntercomEndpoint;
        }

        public bool BelongsToSynod(Uri node)
            => synod.Contains(node);

        public Node LocalNode { get; }

        public IEnumerable<Uri> Synod => synod;

        public TimeSpan HeartBeatInterval { get; }

        public int MissingHeartBeatsBeforeReconnect { get; }

        public Uri IntercomEndpoint { get; }
    }
}