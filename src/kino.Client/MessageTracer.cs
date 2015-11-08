using kino.Diagnostics;
using kino.Framework;
using kino.Messaging;

namespace kino.Client
{
    public class MessageTracer : IMessageTracer
    {
        private readonly ILogger logger;
        public MessageTracer(ILogger logger)
        {
            this.logger = logger;
        }

        public void CallbackRegistered(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                //TODO: Fix commented code below
                logger.Trace($"Callback registered for Message {message.Identity.GetString()}: " +
                             //$"{nameof(message.CallbackIdentity)}:{message.CallbackIdentity.GetString()} " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.CallbackReceiverIdentity)}:{message.CallbackReceiverIdentity.GetString()} " +
                             $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetString()}");
            }
        }

        public void SentToRouter(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Message {message.Identity.GetString()} sent to Router.");
            }
        }

        public void CallbackResultSet(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("Callback set: " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetString()}");
            }
        }

        public void CallbackNotFound(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("Callback not found: " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetString()}");
            }
        }
    }
}