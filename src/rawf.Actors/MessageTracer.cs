using rawf.Diagnostics;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Actors
{
    public class MessageTracer : IMessageTracer
    {
        private readonly ILogger logger;

        public MessageTracer(ILogger logger)
        {
            this.logger = logger;
        }

        public void HandlerNotFound(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("No Actor found for message: " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()}");
            }
        }

        public void MessageProcessed(IMessage message, int responseCount)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("Message processed sync: " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"Number of response messages:{responseCount}");
            }
        }

        public void ResponseSent(IMessage message, bool sentSync)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("Response: " +
                             $"{nameof(sentSync)}:{sentSync}" +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()}");
            }
        }
    }
}