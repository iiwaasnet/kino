using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Actors.Diagnostics
{
    public class ExceptionHandlerActor : IActor
    {
        //public IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition()
        //{
        //    yield return new MessageHandlerDefinition
        //                 {
        //                     Message = new MessageDefinition
        //                               {
        //                                   Identity = ExceptionMessage.MessageIdentity,
        //                                   Version = Message.CurrentVersion
        //                               },
        //                     Handler = HandleException
        //                 };
        //}

        [MessageHandlerDefinition(typeof(ExceptionMessage))]
        private Task<IActorResult> HandleException(IMessage message)
        {
            var payload = message.GetPayload<ExceptionMessage>();

            Console.WriteLine(payload.Exception.ToString());

            return null;
        }
    }
}