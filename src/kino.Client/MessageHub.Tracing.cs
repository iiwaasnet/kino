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
                    logger.Trace($"Callback registered for Message {message}: " +
                                 $"{nameof(message.CallbackPoint)} {messageIdentifier} " +
                                 $"{nameof(message.CallbackReceiverIdentity)}:{message.CallbackReceiverIdentity.GetAnyString()} " +
                                 $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetAnyString()}");
                }
            }
        }

        private void SentToRouter(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Message {message} sent to Router.");
            }
        }

        private void CallbackResultSet(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Callback set. Result message: {message}");
            }
        }

        private void CallbackNotFound(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Callback not found! Result message: {message}");
            }
        }
    }
}