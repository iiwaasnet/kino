using System.Collections.Generic;
using kino.Connectivity;

namespace kino.Actors
{
    public interface IActor
    {
        IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition();
    }
}