using System.Collections.Generic;
using System.Linq;
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
                             Message = new MessageDefinition
                                       {
                                           Identity = HelloMessage.MessageIdentity,
                                           Version = Message.CurrentVersion
                                       }
                         };
        }

        private IMessage StartProcess(IMessage message)
        {
            var hello = message.GetPayload<HelloMessage>();
            //System.Console.WriteLine(hello.Greeting);

            return Message.Create(new EhlloMessage {Ehllo = new string(hello.Greeting.Reverse().ToArray())}, EhlloMessage.MessageIdentity);
        }
    }
}