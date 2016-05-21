using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"{nameof(message.Partition)}:{message.Partition.GetString()}");
            }
        }

        private void MessageProcessed(IMessage message, IEnumerable<IMessage> responses)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("Message processed sync: " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"{nameof(message.Partition)}:{message.Partition.GetString()} " +
                             $"Number of response messages:{responses.Count()}");
            }
        }

        private void ResponseSent(IMessage message, bool sentSync)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("Response: " +
                             $"{nameof(sentSync)}:{sentSync} " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"{nameof(message.Partition)}:{message.Partition.GetString()}");
            }
        }
    }
}