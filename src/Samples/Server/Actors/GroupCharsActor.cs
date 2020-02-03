using System.Linq;
using System.Threading.Tasks;
using kino.Actors;
using kino.Messaging;
using Server.Messages;

namespace Server.Actors
{
    public class GroupCharsActor : Actor
    {
        [MessageHandlerDefinition(typeof(EhlloMessage))]
        private async ValueTask<IActorResult> StartProcess(IMessage message)
        {
            var ehllo = message.GetPayload<EhlloMessage>();

            var messageOut = Message.Create(new GroupCharsResponseMessage
                                            {
                                                Groups = ehllo.Ehllo.GroupBy(c => c).Select(g => new GroupInfo {Char = g.Key, Count = g.Count()}),
                                                Text = ehllo.Ehllo
                                            });

            return new ActorResult(messageOut);
        }
    }
}