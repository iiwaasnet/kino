using System.Threading.Tasks;
using kino.Actors;
using kino.Messaging;

namespace kino.Tests.Actors.Setup
{
    public class NullActor : Actor
    {
        [MessageHandlerDefinition(typeof(NullMessage))]
        private Task<IActorResult> Process(IMessage messageIn)
        {
            var message = messageIn.GetPayload<NullMessage>();

            return null;
        }
    }
}