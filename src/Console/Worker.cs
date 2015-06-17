using System.Collections.Generic;
using Console.Messages;

namespace Console
{
    public class Worker : IWorker
    {
        public IEnumerable<MessageMap> GetInterfaceDefinition()
        {
            yield return
                new MessageMap
                {
                    Handler = StartProcess,
                    Message = new MessageDefinition {Type = StartProcessMessage.MessageIdentity}
                };
        }

        private IMessage StartProcess(IMessage startProcessArgs)
        {
            return null;
        }
    }
}