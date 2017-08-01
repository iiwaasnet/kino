using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core;
using kino.Core.Framework;

namespace kino.Consensus.Configuration
{
    public class SynodConfigurationProvider : ISynodConfigurationProvider
    {
        private volatile IEnumerable<Location> synod;

        public SynodConfigurationProvider(SynodConfiguration config)
        {
            LocalNode = new Node(config.LocalNode.ParseAddress(), ReceiverIdentifier.CreateIdentity());
            synod = config.Members
                          .Select(uri => new Location(uri))
                          .ToList();
            HeartBeatInterval = config.HeartBeatInterval;
            MissingHeartBeatsBeforeReconnect = config.MissingHeartBeatsBeforeReconnect;
            IntercomEndpoint = new Uri(config.IntercomEndpoint);
        }

        //public void ForceNodeIpAddressResolution(Uri nodeUri)
        //{
        //    AssertNodeIsNotLocal();

        //    var node = synod.FirstOrDefault(n => n.Uri == nodeUri);
        //    if (node != null)
        //    {
        //        node.RefreshLocation();
        //    }
        //    else
        //    {
        //        throw new Exception($"Node {nodeUri} doesn't belong to synod!");
        //    }

        //    void AssertNodeIsNotLocal()
        //    {
        //        if (LocalNode.Uri == nodeUri)
        //        {
        //            throw new Exception($"Can't force {nameof(LocalNode)} IP address {LocalNode.Uri} resolution!");
        //        }
        //    }
        //}

        public bool BelongsToSynod(Uri node)
            => synod.Any(n => n.Uri == node);

        public Node LocalNode { get; }

        public IEnumerable<Location> Synod => synod;

        public TimeSpan HeartBeatInterval { get; }

        public int MissingHeartBeatsBeforeReconnect { get; }

        public Uri IntercomEndpoint { get; }
    }
}