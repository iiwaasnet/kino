using kino.Core.Framework;
using kino.Core.Messaging;

namespace kino.Actors
{
    public partial class ActorHost
    {
        private void HandlerNotFound(IMessage message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("No Actor found for message: " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()}");
            }
        }

        private void MessageProcessed(IMessage message, int responseCount)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("Message processed sync: " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"Number of response messages:{responseCount}");
            }
        }

        private void ResponseSent(IMessage message, bool sentSync)
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