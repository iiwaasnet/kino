using System.Collections.Generic;
using Console.Messages;

namespace Console
{
    public class Actor : IActor
    {
        public IEnumerable<MessageMap> GetInterfaceDefinition()
        {
            yield return new MessageMap
                         {
                             Handler = StartProcess,
                             Message = new MessageDefinition {Type = StartProcessMessage.MessageIdentity}
                         };
        }

        private IMessage StartProcess(IMessage message)
        {
            var startProcess = message.GetPayload<StartProcessMessage>();
            System.Console.WriteLine(startProcess.Arg);

            return null;
        }
    }
}