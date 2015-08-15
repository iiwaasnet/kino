using System;
using System.Collections.Generic;
using rawf.Connectivity;

namespace rawf.Consensus
{
    public class SynodConfiguration : ISynodConfiguration
    {
        private readonly HashSet<Uri> synod;

        public SynodConfiguration(ISynodConfigurationProvider configProvider)
        {
            LocalNode = new Node(configProvider.LocalNode, SocketIdentifier.CreateNew());
            synod = new HashSet<Uri>(configProvider.Synod); 
        }

        public INode LocalNode { get; }

        public IEnumerable<Uri> Synod => synod;

        public bool BelongsToSynod(Uri node)
            => synod.Contains(node);
    }
}