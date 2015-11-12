using System;
using System.Collections.Generic;
using System.Linq;
using kino.Consensus.Configuration;

namespace kino.Rendezvous.Configuration
{
    public class SynodConfigurationProvider : ISynodConfigurationProvider
    {
        public SynodConfigurationProvider(ApplicationConfiguration config)
        {
            LocalNode = new Uri(config.Synod.LocalNode);
            Synod = config.Synod.Members.Select(m => new Uri(m));
        }

        public Uri LocalNode { get; }
        public IEnumerable<Uri> Synod { get; }
    }
}