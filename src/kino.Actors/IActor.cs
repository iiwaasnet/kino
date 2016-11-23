using System.Collections.Generic;
using kino.Core;

namespace kino.Actors
{
    public interface IActor
    {
        IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition();

        ReceiverIdentifier Identifier { get; }
    }
}