using System.Linq;
using System.Threading.Tasks;
using kino.Actors;
using kino.Connectivity;
using kino.Messaging;
using Server.Messages;

namespace Server.Actors
{
    public class RevertStringActor : IActor
    {
        //public IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition()
        //{
        //    yield return new MessageHandlerDefinition
        //                 {
        //                     Handler = StartProcess,
        //                     Message = MessageDefinition.Create<HelloMessage>()
        //                 };
        //}

        [MessageHandlerDefinition(typeof (HelloMessage))]
        private async Task<IActorResult> StartProcess(IMessage message)
        {
            var hello = message.GetPayload<HelloMessage>();
            //System.Console.WriteLine(hello.Greeting);

            //return await Task.Delay(0)
            //                 .ContinueWith(_ => Message.Create(new EhlloMessage
            //                                                   {
            //                                                       Ehllo = new string(hello.Greeting.Reverse().ToArray())
            //                                                   },
            //                                                   EhlloMessage.MessageIdentity))
            //                                                   .ConfigureAwait(false);

            //throw new Exception("Bla!");

            //Thread.Sleep(50000);

            var reversedString = new string(hello.Greeting.Reverse().ToArray());

            //WriteLine(reversedString);

            return new ActorResult(Message.Create(new EhlloMessage {Ehllo = reversedString}));
        }
    }
}