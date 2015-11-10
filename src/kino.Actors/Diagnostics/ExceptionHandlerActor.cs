using System;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Actors.Diagnostics
{
    public class ExceptionHandlerActor : Actor
    {
        [MessageHandlerDefinition(typeof (ExceptionMessage))]
        private Task<IActorResult> HandleException(IMessage message)
        {
            var payload = message.GetPayload<ExceptionMessage>();

            Console.WriteLine(payload.Exception.ToString());

            return null;
        }
    }
}