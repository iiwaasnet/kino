using System.Threading.Tasks;
using kino.Core.Diagnostics;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Actors.Diagnostics
{
    public class ExceptionHandlerActor : Actor
    {
        private readonly ILogger logger;

        public ExceptionHandlerActor(ILogger logger)
        {
            this.logger = logger;
        }

        [MessageHandlerDefinition(typeof(ExceptionMessage))]
        public Task<IActorResult> HandleException(IMessage message)
        {
            var payload = message.GetPayload<ExceptionMessage>();

            logger.Error(payload.Exception);

            return null;
        }
    }
}