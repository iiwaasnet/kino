using System.Collections.Generic;
using System.Linq;
using kino.Messaging;

namespace kino.Actors
{
    public partial class ActorHost
    {
        private void HandlerNotFound(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"No Actor found for message: {message}");
            }
        }

        private void MessageProcessed(IMessage message, IEnumerable<IMessage> responses)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Message processed sync: {message} " +
                             $"Number of response messages:{responses.Count()}");
            }
        }

        private void ResponseSent(IMessage message, bool sentSync)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Response: {nameof(sentSync)}:{sentSync} {message}");
            }
        }
    }
}