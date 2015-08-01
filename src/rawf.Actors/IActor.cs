using System.Collections.Generic;
using rawf.Connectivity;

namespace rawf.Actors
{
    public interface IActor
    {
        IEnumerable<MessageMap> GetInterfaceDefinition();
    }
}