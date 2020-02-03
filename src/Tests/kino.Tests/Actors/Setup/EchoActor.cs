using System.Threading.Tasks;
using kino.Actors;
using kino.Messaging;

namespace kino.Tests.Actors.Setup
{
    public class EchoActor : Actor
    {
        [MessageHandlerDefinition(typeof(SimpleMessage))]
        private async ValueTask<IActorResult> Process(IMessage messageIn)
        {
            var messageOut = Message.Create(new SimpleMessage
                                            {
                                                Content = messageIn.GetPayload<SimpleMessage>().Content,
                                                Partition = messageIn.Partition
                                            },
                                            messageIn.Distribution);

            return new ActorResult(messageOut);
        }

        [MessageHandlerDefinition(typeof(AsyncMessage))]
        private async ValueTask<IActorResult> AsyncProcess(IMessage messageIn)
        {
            var delay = messageIn.GetPayload<AsyncMessage>().Delay;
            var messageOut = Message.Create(new AsyncMessage
                                            {
                                                Delay = delay,
                                                Partition = messageIn.Partition
                                            },
                                            messageIn.Distribution);

            return new ActorResult(await Task.Delay(delay).ContinueWith(_ => messageOut).ConfigureAwait(false));
        }

        [MessageHandlerDefinition(typeof(LocalMessage), keepRegistrationLocal: true)]
        private async ValueTask<IActorResult> ProcessLocalMessage(IMessage messageIn)
            => null;
    }
}