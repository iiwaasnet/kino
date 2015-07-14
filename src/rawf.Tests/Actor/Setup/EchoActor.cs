using System.Collections.Generic;
using System.Threading.Tasks;
using rawf.Actors;
using rawf.Messaging;

namespace rawf.Tests.Actor.Setup
{
    public class EchoActor : IActor
    {
        public IEnumerable<MessageMap> GetInterfaceDefinition()
        {
            yield return new MessageMap
                         {
                             Message = new MessageDefinition
                                       {
                                           Identity = EmptyMessage.MessageIdentity,
                                           Version = Message.CurrentVersion
                                       },
                             Handler = Process
                         };
        }

        private Task<IMessage> Process(IMessage messageIn)
        {
            return Task.FromResult(messageIn);
        }
    }
}