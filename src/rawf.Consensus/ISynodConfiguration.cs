using System;
using System.Collections.Generic;

namespace rawf.Consensus
{
    public interface ISynodConfiguration
    {
        INode LocalNode { get; }
        IEnumerable<Uri> Synod { get; }

        bool BelongsToSynod(Uri node);
    }
}