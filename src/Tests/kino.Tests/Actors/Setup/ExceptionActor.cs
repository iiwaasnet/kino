using System;
using System.Threading.Tasks;
using kino.Actors;
using kino.Core.Connectivity;
using kino.Core.Messaging;

namespace kino.Tests.Actors.Setup
{
    public class ExceptionActor : Actor
    {
        [MessageHandlerDefinition(typeof (SimpleMessage))]
        private async Task<IActorResult> Process(IMessage messageIn)
        {
            var message = messageIn.GetPayload<SimpleMessage>().Content;

            throw new Exception(message);
        }

        [MessageHandlerDefinition(typeof (AsyncExceptionMessage))]
        private async Task<IActorResult> AsyncProcess(IMessage messageIn)
        {
            var error = messageIn.GetPayload<AsyncExceptionMessage>();

            await Task.Delay(error.Delay).ContinueWith(_ => { throw new Exception(error.ErrorMessage); }).ConfigureAwait(false);

            return null;
        }
    }
}