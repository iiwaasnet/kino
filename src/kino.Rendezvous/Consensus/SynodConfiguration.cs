using System;
using System.Collections.Generic;
using kino.Connectivity;

namespace kino.Rendezvous.Consensus
{
    public class SynodConfiguration : ISynodConfiguration
    {
        private readonly HashSet<Uri> synod;

        public SynodConfiguration(ISynodConfigurationProvider configProvider)
        {
            LocalNode = new Node(configProvider.LocalNode, SocketIdentifier.CreateIdentity());
            synod = new HashSet<Uri>(configProvider.Synod); 
        }

        public Node LocalNode { get; }

        public IEnumerable<Uri> Synod => synod;

        public bool BelongsToSynod(Uri node)
            => synod.Contains(node);
    }
}