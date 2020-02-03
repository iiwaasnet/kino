using System.Threading.Tasks;
using kino.Actors;
using kino.Messaging;
using Server.Messages;

namespace Server.Actors
{
    public class RevertStringActor : Actor
    {
        [MessageHandlerDefinition(typeof(HelloMessage))]
        private async ValueTask<IActorResult> StartProcess(IMessage message)
        {
            //Console.WriteLine($"Received by {Identifier}");
            var hello = message.GetPayload<HelloMessage>();

            //var reversedString = new string(hello.Greeting.Reverse().ToArray());

            //return new ActorResult(Message.Create(new EhlloMessage {Ehllo = reversedString},
            //                                      securityProvider.GetDomain(Ehhlo.Identity)));

            return new ActorResult(Message.Create(new EhlloMessage { Ehllo = hello.Greeting }));

            return ActorResult.NoWait.Result;
        }
    }
}