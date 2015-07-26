using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using rawf.Backend;
using rawf.Messaging;

namespace rawf.Tests.Actor.Setup
{
    public class ExceptionActor : IActor
    {
        public ExceptionActor()
        {
        }

        public IEnumerable<MessageMap> GetInterfaceDefinition()
        {
            yield return new MessageMap
                         {
                             Message = new MessageDefinition
                                       {
                                           Identity = SimpleMessage.MessageIdentity,
                                           Version = Message.CurrentVersion
                                       },
                             Handler = Process
                         };
            yield return new MessageMap
                         {
                             Message = new MessageDefinition
                                       {
                                           Identity = AsyncExceptionMessage.MessageIdentity,
                                           Version = Message.CurrentVersion
                                       },
                             Handler = AsyncProcess
                         };
        }

        private async Task<IMessage> Process(IMessage messageIn)
        {
            var message = messageIn.GetPayload<SimpleMessage>().Message;

            throw new Exception(message);
        }

        private async Task<IMessage> AsyncProcess(IMessage messageIn)
        {
            var error = messageIn.GetPayload<AsyncExceptionMessage>();

            await Task.Delay(error.Delay).ContinueWith(_ => { throw new Exception(error.ErrorMessage); }).ConfigureAwait(false);

            return null;
        }
    }
}