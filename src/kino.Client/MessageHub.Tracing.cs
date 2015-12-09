using kino.Core.Framework;
using kino.Core.Messaging;

namespace kino.Client
{
    public partial class MessageHub
    {
        private void CallbackRegistered(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                foreach (var messageIdentifier in message.CallbackPoint)
                {
                    logger.Trace($"Callback registered for Message {message.Identity.GetString()}: " +
                                 $"{nameof(message.CallbackPoint)}:{messageIdentifier.Identity.GetString()}-{messageIdentifier.Version.GetString()}" +
                                 $"{nameof(message.Version)}:{message.Version.GetString()} " +
                                 $"{nameof(message.CallbackReceiverIdentity)}:{message.CallbackReceiverIdentity.GetString()} " +
                                 $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetString()}");
                }
            }
        }

        private void SentToRouter(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Message {message.Identity.GetString()} sent to Router.");
            }
        }

        private void CallbackResultSet(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("Callback set: " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetString()}");
            }
        }

        private void CallbackNotFound(IMessage message)
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