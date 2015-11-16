using System.Collections.Generic;
using kino.Core.Connectivity;

namespace kino.Client
{
    public interface ICallbackPoint
    {
        IEnumerable<MessageIdentifier> MessageIdentifiers { get; }
    }
}