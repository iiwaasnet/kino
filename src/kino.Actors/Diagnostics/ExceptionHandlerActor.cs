using System;
using System.Threading.Tasks;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;

namespace kino.Actors.Diagnostics
{
    public class ExceptionHandlerActor : Actor
    {
        private readonly ILogger logger;

        public ExceptionHandlerActor(ILogger logger)
        {
            this.logger = logger;
        }

        [MessageHandlerDefinition(typeof (ExceptionMessage))]
        public async Task<IActorResult> HandleException(IMessage message)
        {
            var payload = message.GetPayload<ExceptionMessage>();

            logger.Error(payload.Exception);

            return null;
        }
    }
}