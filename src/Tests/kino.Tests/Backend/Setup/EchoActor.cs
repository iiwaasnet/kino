using System.Collections.Generic;
using System.Threading.Tasks;
using kino.Actors;
using kino.Connectivity;
using kino.Messaging;

namespace kino.Tests.Backend.Setup
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

        private async Task<IActorResult> Process(IMessage messageIn)
        {
            return new ActorResult(messageIn);
        }

        private async Task<IActorResult> AsyncProcess(IMessage messageIn)
        {
            var delay = messageIn.GetPayload<AsyncMessage>().Delay;

            return new ActorResult(await Task.Delay(delay).ContinueWith(_=> messageIn).ConfigureAwait(false));
        }
    }
}