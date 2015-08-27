using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using rawf.Connectivity;
using rawf.Messaging;
using rawf.Messaging.Messages;

namespace rawf.Actors.Diagnostics
{
    public class ExceptionHandlerActor : IActor
    {
        public IEnumerable<MessageMap> GetInterfaceDefinition()
        {
            yield return new MessageMap
                         {
                             Message = new MessageDefinition
                                       {
                                           Identity = ExceptionMessage.MessageIdentity,
                                           Version = Message.CurrentVersion
                                       },
                             Handler = HandleException
                         };
        }

        private Task<IActorResult> HandleException(IMessage message)
        {
            var payload = message.GetPayload<ExceptionMessage>();

            Console.WriteLine(payload.Exception.ToString());

            return null;
        }
    }
}