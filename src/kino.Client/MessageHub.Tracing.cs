using kino.Core.Connectivity;
using kino.Core.Framework;
using kino.Core.Messaging;

namespace kino.Client
{
    public partial class MessageHub
    {
        private void CallbackRegistered(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                foreach (var messageIdentifier in message.CallbackPoint)
                {
                    logger.Trace($"Callback registered for Message {new MessageIdentifier(message)}: " +
                                 $"{nameof(message.CallbackPoint)} {messageIdentifier} " +
                                 $"{nameof(message.CallbackReceiverIdentity)}:{message.CallbackReceiverIdentity.GetString()} " +
                                 $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetString()}");
                }
            }
        }

        private void SentToRouter(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Message {new MessageIdentifier(message)} sent to Router.");
            }
        }

        private void CallbackResultSet(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Callback set: {new MessageIdentifier(message)} " +
                             $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetString()}");
            }
        }

        private void CallbackNotFound(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Callback not found: {new MessageIdentifier(message)} " +
                             $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetString()}");
            }
        }
    }
}