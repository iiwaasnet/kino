using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using rawf.Messaging;
using rawf.Messaging.Messages;

namespace rawf.Backend.Diagnostics
{
    public class ExceptionHandlerActor : IActor
    {
        private Task<IMessage> HandleException(IMessage message)
        {
            var payload = message.GetPayload<ExceptionMessage>();

            Console.WriteLine(payload.Exception.ToString());

            return null;
        }

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
    }
}