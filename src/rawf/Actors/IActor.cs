using System.Collections.Generic;

namespace rawf.Actors
{
    public interface IActor
    {
        IEnumerable<MessageMap> GetInterfaceDefinition();
    }
}