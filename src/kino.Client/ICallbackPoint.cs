using System.Collections.Generic;
using kino.Connectivity;

namespace kino.Client
{
    public interface ICallbackPoint
    {
        IEnumerable<MessageIdentifier> MessageIdentifiers { get; }
    }
}