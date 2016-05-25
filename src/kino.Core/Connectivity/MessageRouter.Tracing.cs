using System.Linq;
using kino.Core.Framework;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public partial class MessageRouter
    {
        private void RoutedToLocalActor(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Message: {message} " +
                             $"routed to {nameof(message.SocketIdentity)}:{message.SocketIdentity.GetString()}");
            }
        }

        private void ForwardedToOtherNode(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Message: {message} " +
                             $"forwarded to other node {nameof(message.SocketIdentity)}:{message.SocketIdentity.GetString()}");
            }
        }

        private void ReceivedFromOtherNode(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                var hops = string.Join("|",
                                       message
                                           .GetMessageRouting()
                                           .Select(h => $"{nameof(h.Uri)}:{h.Uri.ToSocketAddress()}/{h.Identity.GetString()}"));

                logger.Trace($"Message: {message} received from other node via hops {hops}");
            }
        }
    }
}