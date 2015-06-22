using System.Collections.Generic;

namespace Console
{
    public interface IActor
    {
        IEnumerable<MessageMap> GetInterfaceDefinition();
    }
}