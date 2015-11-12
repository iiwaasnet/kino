using System.Linq;
using System.Threading.Tasks;
using kino.Actors;
using kino.Connectivity;
using kino.Messaging;
using Server.Messages;

namespace Server.Actors
{
    public class RevertStringActor : Actor
    {
        [MessageHandlerDefinition(typeof (HelloMessage))]
        private async Task<IActorResult> StartProcess(IMessage message)
        {
            var hello = message.GetPayload<HelloMessage>();

            var reversedString = new string(hello.Greeting.Reverse().ToArray());

            return new ActorResult(Message.Create(new EhlloMessage {Ehllo = reversedString}));
        }
    }
}