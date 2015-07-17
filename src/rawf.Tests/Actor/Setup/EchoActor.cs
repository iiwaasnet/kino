using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using rawf.Actors;
using rawf.Messaging;

namespace rawf.Tests.Actor.Setup
{
    public class EchoActor : IActor
    {
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
                                           Identity = AsyncMessage.MessageIdentity,
                                           Version = Message.CurrentVersion
                                       },
                             Handler = AsyncProcess
                         };
        }

        private async Task<IMessage> Process(IMessage messageIn)
        {
            return messageIn;
        }

        private async Task<IMessage> AsyncProcess(IMessage messageIn)
        {
            var delay = messageIn.GetPayload<AsyncMessage>().Delay;

            return await Task.Delay(delay).ContinueWith(_=> messageIn).ConfigureAwait(false);
        }
    }
}