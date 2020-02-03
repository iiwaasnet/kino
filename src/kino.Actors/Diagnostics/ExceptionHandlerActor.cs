using System.Threading.Tasks;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Actors.Diagnostics
{
    public class ExceptionHandlerActor : Actor
    {
        private readonly ILogger logger;

        public ExceptionHandlerActor(ILogger logger)
            => this.logger = logger;

        [MessageHandlerDefinition(typeof(ExceptionMessage), keepRegistrationLocal: true)]
        public ValueTask<IActorResult> HandleException(IMessage message)
        {
            var payload = message.GetPayload<ExceptionMessage>();

            logger.Error(new KinoException(payload.Message, payload.ExceptionType, payload.StackTrace));

            return default;
        }
    }
}