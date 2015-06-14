using System.Collections.Generic;

namespace Console
{
    public interface IWorker
    {
        IEnumerable<MessageMap> GetInterfaceDefinition();
    }
}