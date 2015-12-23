using System;
using System.Collections.Generic;
using System.Linq;
using kino.Consensus.Configuration;

namespace kino.Rendezvous.Configuration
{
    public class SynodConfigurationProvider : ISynodConfigurationProvider
    {
        public SynodConfigurationProvider(SynodConfiguration config)
        {
            LocalNode = new Uri(config.LocalNode);
            Synod = config.Members.Select(m => new Uri(m));
        }

        public Uri LocalNode { get; }

        public IEnumerable<Uri> Synod { get; }
    }
}