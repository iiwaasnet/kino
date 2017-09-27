using kino.Core.Framework;
using kino.Messaging;
using Microsoft.Extensions.Logging;

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
                    logger.LogTrace($"Callback [{message.CallbackKey}] registered for Message {message}: " +
                                 $"{nameof(message.CallbackPoint)} {messageIdentifier} " +
                                 $"{nameof(message.CallbackReceiverIdentity)}:{message.CallbackReceiverIdentity.GetAnyString()} " +
                                 $"{nameof(message.CorrelationId)}:{message.CorrelationId.GetAnyString()}");
                }
            }
        }

        private void SentToRouter(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.LogTrace($"Message {message} with Callback [{message.CallbackKey}] sent to Router.");
            }
        }

        private void CallbackResultSet(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.LogTrace($"Callback [{message.CallbackKey}] set. Result message: {message}");
            }
        }

        private void CallbackNotFound(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.LogTrace($"Callback [{message.CallbackKey}] not found! Result message: {message}");
            }
        }
    }
}