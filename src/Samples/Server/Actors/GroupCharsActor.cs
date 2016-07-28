using System.Linq;
using System.Threading.Tasks;
using kino.Actors;
using kino.Core.Connectivity;
using kino.Core.Messaging;
using kino.Core.Security;
using Server.Messages;

namespace Server.Actors
{
    public class GroupCharsActor : Actor
    {
        private readonly ISecurityProvider securityProvider;
        private static readonly MessageIdentifier GroupCharResponse = MessageIdentifier.Create<GroupCharsResponseMessage>();

        public GroupCharsActor(ISecurityProvider securityProvider)
        {
            this.securityProvider = securityProvider;
        }

        [MessageHandlerDefinition(typeof(EhlloMessage))]
        private async Task<IActorResult> StartProcess(IMessage message)
        {
            var ehllo = message.GetPayload<EhlloMessage>();

            var messageOut = Message.Create(new GroupCharsResponseMessage
                                            {
                                                Groups = ehllo.Ehllo.GroupBy(c => c).Select(g => new GroupInfo {Char = g.Key, Count = g.Count()}),
                                                Text = ehllo.Ehllo
                                            },
                                            securityProvider.GetSecurityDomain(GroupCharResponse.Identity));

            return new ActorResult(messageOut);
        }
    }
}