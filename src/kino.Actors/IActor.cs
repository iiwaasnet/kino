using System.Collections.Generic;

namespace kino.Actors
{
    public interface IActor
    {
        IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition();
    }
}