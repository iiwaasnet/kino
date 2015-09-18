using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using kino.Actors;
using kino.Connectivity;
using kino.Messaging;
using Server.Messages;

namespace Server.Actors
{
    public class GroupCharsActor : IActor
    {
        public IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition()
        {
            yield return new MessageHandlerDefinition
                         {
                             Handler = StartProcess,
                             Message = new MessageDefinition
                                       {
                                           Identity = EhlloMessage.MessageIdentity,
                                           Version = Message.CurrentVersion
                                       }
                         };
        }

        private async Task<IActorResult> StartProcess(IMessage message)
        {
            var ehllo = message.GetPayload<EhlloMessage>();

            var messageOut = Message.Create(new GroupCharsResponseMessage
                                            {
                                                Groups = ehllo.Ehllo.GroupBy(c => c).Select(g => new GroupInfo {Char = g.Key, Count = g.Count()}),
                                                Text = ehllo.Ehllo
                                            },
                                            GroupCharsResponseMessage.MessageIdentity);

            return new ActorResult(messageOut);
        }
    }
}