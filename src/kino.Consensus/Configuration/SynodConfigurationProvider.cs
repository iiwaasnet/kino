using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core;
using kino.Core.Framework;

namespace kino.Consensus.Configuration
{
    public class SynodConfigurationProvider : ISynodConfigurationProvider
    {
        private volatile IDictionary<Uri, string> synod;

        public SynodConfigurationProvider(SynodConfiguration config)
        {
            LocalNode = new Node(config.LocalNode.ParseAddress(), ReceiverIdentifier.CreateIdentity());
            synod = config.Members.ToDictionary(m => m.ParseAddress(), m => m);
            HeartBeatInterval = config.HeartBeatInterval;
            MissingHeartBeatsBeforeReconnect = config.MissingHeartBeatsBeforeReconnect;
            IntercomEndpoint = new Uri(config.IntercomEndpoint);
        }

        public void ForceNodeIpAddressResolution(Uri nodeUri)
        {
            AssertNodeIsNotLocal();

            if (synod.TryGetValue(nodeUri, out var nodeConfiguredUri))
            {
                var tmp = synod.Where(kvp => kvp.Key != nodeUri)
                               .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                tmp[nodeConfiguredUri.ParseAddress()] = nodeConfiguredUri;
                synod = tmp;
            }
            else
            {
                throw new Exception($"Node {nodeUri} doesn't belong to synod!");
            }

            void AssertNodeIsNotLocal()
            {
                if (LocalNode.Uri == nodeUri)
                {
                    throw new Exception($"Can't force {nameof(LocalNode)} IP address {LocalNode.Uri} resolution!");
                }
            }
        }

        public bool BelongsToSynod(Uri node)
            => synod.ContainsKey(node);

        public Node LocalNode { get; }

        public IEnumerable<Uri> Synod => synod.Keys;

        public TimeSpan HeartBeatInterval { get; }

        public int MissingHeartBeatsBeforeReconnect { get; }

        public Uri IntercomEndpoint { get; }
    }
}