using kino.Core.Framework;
using kino.Messaging;

namespace kino.Client
{
    public partial class MessageHub
    {
        private void CallbackRegistered(Message message)
        {
            if ((message.TraceOptions & MessageTraceOptions.Routing) == MessageTraceOptions.Routing)
            {
                foreach (var messageIdentifier in message.CallbackPoint)
                {
                    logger.Trace($"Callback [{message.CallbackKey}] registered for Message {message}: " +
                                 $"{nameof(message.CallbackPoint)} {messageIdentifier} " +
                                 $"{nameof(message.CallbackReceiverIdentity)}:{message.CallbackReceiverIdentity.GetAnyString()} " +
                                 $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetAnyString()}");
                }
            }
        }

        private void SentToRouter(Message message)
        {
            if ((message.TraceOptions & MessageTraceOptions.Routing) == MessageTraceOptions.Routing)
            {
                logger.Trace($"Message {message} with Callback [{message.CallbackKey}] sent to Router.");
            }
        }

        private void CallbackResultSet(Message message)
        {
            if ((message.TraceOptions & MessageTraceOptions.Routing) == MessageTraceOptions.Routing)
            {
                logger.Trace($"Callback [{message.CallbackKey}] set. Result message: {message}");
            }
        }

        private void CallbackNotFound(Message message)
        {
            if ((message.TraceOptions & MessageTraceOptions.Routing) == MessageTraceOptions.Routing)
            {
                logger.Trace($"Callback [{message.CallbackKey}] not found! Result message: {message}");
            }
        }
    }
}