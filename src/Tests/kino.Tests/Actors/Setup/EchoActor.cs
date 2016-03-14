using System.Threading.Tasks;
using kino.Actors;
using kino.Core.Connectivity;
using kino.Core.Messaging;

namespace kino.Tests.Actors.Setup
{
    public class EchoActor : Actor
    {
        [MessageHandlerDefinition(typeof (SimpleMessage))]
        private async Task<IActorResult> Process(IMessage messageIn)
        {
            return new ActorResult(messageIn);
        }

        [MessageHandlerDefinition(typeof (AsyncMessage))]
        private async Task<IActorResult> AsyncProcess(IMessage messageIn)
        {
            var delay = messageIn.GetPayload<AsyncMessage>().Delay;

            return new ActorResult(await Task.Delay(delay).ContinueWith(_ => messageIn).ConfigureAwait(false));
        }

        [MessageHandlerDefinition(typeof (LocalMessage), keepRegistrationLocal: true)]
        private async Task<IActorResult> ProcessLocalMessage(IMessage messageIn)
        {
            return null;
        }
    }
}