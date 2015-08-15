using System;
using System.Collections.Generic;

namespace rawf.Consensus
{
    public interface ISynodConfigurationProvider
    {
        Uri LocalNode { get; }
        IEnumerable<Uri> Synod { get; }
    }
}