using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using rawf.Actors;
using rawf.Messaging;

namespace rawf.Tests.Actor.Setup
{
    public class ExceptionActor : IActor
    {
        private readonly string exceptionMessage;
        public ExceptionActor(string exceptionMessage)
        {
            this.exceptionMessage = exceptionMessage;
        }

        public IEnumerable<MessageMap> GetInterfaceDefinition()
        {
            yield return new MessageMap
                         {
                             Message = new MessageDefinition
                                       {
                                           Identity = EmptyMessage.MessageIdentity,
                                           Version = Message.CurrentVersion
                                       },
                             Handler = Process
                         };
        }

        private async Task<IMessage> Process(IMessage messageIn)
        {
            throw new Exception(exceptionMessage);
        }
    }
}