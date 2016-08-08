using System.Linq;
using System.Threading.Tasks;
using kino.Actors;
using kino.Core.Connectivity;
using kino.Core.Messaging;
using kino.Core.Security;
using Server.Messages;

namespace Server.Actors
{
    public class RevertStringActor : Actor
    {
        private readonly ISecurityProvider securityProvider;
        private static readonly MessageIdentifier Ehhlo = MessageIdentifier.Create<EhlloMessage>();

        public RevertStringActor(ISecurityProvider securityProvider)
        {
            this.securityProvider = securityProvider;
        }

        [MessageHandlerDefinition(typeof(HelloMessage))]
        private async Task<IActorResult> StartProcess(IMessage message)
        {
            var hello = message.GetPayload<HelloMessage>();

            var reversedString = new string(hello.Greeting.Reverse().ToArray());

            return new ActorResult(Message.Create(new EhlloMessage {Ehllo = reversedString},
                                                  securityProvider.GetDomain(Ehhlo.Identity)));
        }
    }
}