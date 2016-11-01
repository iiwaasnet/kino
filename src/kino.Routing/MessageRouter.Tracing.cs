using System.Linq;
using kino.Core.Framework;
using kino.Messaging;

namespace kino.Routing
{
    public partial class MessageRouter
    {
        private void RoutedToLocalActor(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Message: {message} " +
                             $"routed to {nameof(message.SocketIdentity)}:{message.SocketIdentity.GetAnyString()}");
            }
        }

        private void ForwardedToOtherNode(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace($"Message: {message} " +
                             $"forwarded to other node {nameof(message.SocketIdentity)}:{message.SocketIdentity.GetAnyString()}");
            }
        }

        private void ReceivedFromOtherNode(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                var hops = string.Join("|",
                                       message
                                           .GetMessageRouting()
                                           .Select(h => $"{nameof(h.Uri)}:{h.Uri.ToSocketAddress()}/{h.Identity.GetAnyString()}"));

                logger.Trace($"Message: {message} received from other node via hops {hops}");
            }
        }
    }
}