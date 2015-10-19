using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using kino.Actors;
using kino.Connectivity;
using kino.Messaging;

namespace kino.Tests.Actors.Setup
{
    public class ExceptionActor : IActor
    {
        //public IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition()
        //{
        //    yield return new MessageHandlerDefinition
        //                 {
        //                     Message = new MessageDefinition
        //                               {
        //                                   Identity = SimpleMessage.MessageIdentity,
        //                                   Version = Message.CurrentVersion
        //                               },
        //                     Handler = Process
        //                 };
        //    yield return new MessageHandlerDefinition
        //                 {
        //                     Message = new MessageDefinition
        //                               {
        //                                   Identity = AsyncExceptionMessage.MessageIdentity,
        //                                   Version = Message.CurrentVersion
        //                               },
        //                     Handler = AsyncProcess
        //                 };
        //}

        [MessageHandlerDefinition(typeof(SimpleMessage))]
        private async Task<IActorResult> Process(IMessage messageIn)
        {
            var message = messageIn.GetPayload<SimpleMessage>().Content;

            throw new Exception(message);
        }

        [MessageHandlerDefinition(typeof(AsyncExceptionMessage))]
        private async Task<IActorResult> AsyncProcess(IMessage messageIn)
        {
            var error = messageIn.GetPayload<AsyncExceptionMessage>();

            await Task.Delay(error.Delay).ContinueWith(_ => { throw new Exception(error.ErrorMessage); }).ConfigureAwait(false);

            return null;
        }
    }
}