using System.Collections.Generic;

namespace rawf.Backend
{
    public interface IActor
    {
        IEnumerable<MessageMap> GetInterfaceDefinition();
    }
}