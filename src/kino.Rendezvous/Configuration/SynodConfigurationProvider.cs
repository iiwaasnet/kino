using System;
using System.Collections.Generic;
using kino.Consensus.Configuration;

namespace kino.Rendezvous.Configuration
{
    public class SynodConfigurationProvider : ISynodConfigurationProvider
    {
        public SynodConfigurationProvider(SynodConfiguration config)
        {
            LocalNode = config.LocalNode;
            Synod = config.Members;
        }

        public Uri LocalNode { get; }

        public IEnumerable<Uri> Synod { get; }
    }
}