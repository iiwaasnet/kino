using System.Collections.Generic;
using kino.Core.Connectivity;

namespace kino.Actors
{
    public interface IActor
    {
        IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition();
    }
}