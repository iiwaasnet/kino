using kino.Core.Framework;
using kino.Messaging;

namespace kino.Routing
{
    public partial class MessageRouter
    {
        private void RoutedToLocalActor(Message message)
        {
            if ((message.TraceOptions & MessageTraceOptions.Routing) == MessageTraceOptions.Routing)
            {
                logger.Trace($"Message: {message} " +
                             $"routed to {nameof(message.SocketIdentity)}:{message.SocketIdentity.GetAnyString()}");
            }
        }

        private void ForwardedToOtherNode(Message message)
        {
            if ((message.TraceOptions & MessageTraceOptions.Routing) == MessageTraceOptions.Routing)
            {
                logger.Trace($"Message: {message} " +
                             $"forwarded to other node {nameof(message.SocketIdentity)}:{message.SocketIdentity.GetAnyString()}");
            }
        }
    }
}