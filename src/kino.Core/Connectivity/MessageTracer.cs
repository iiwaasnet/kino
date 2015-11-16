using System.Linq;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class MessageTracer : IMessageTracer
    {
        private readonly ILogger logger;

        public MessageTracer(ILogger logger)
        {
            this.logger = logger;
        }

        public void RoutedToLocalActor(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("Message: " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"{nameof(message.Distribution)}:{message.Distribution} " +
                             $"routed to {nameof(message.SocketIdentity)}:{message.SocketIdentity.GetString()}");
            }
        }

        public void ForwardedToOtherNode(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                logger.Trace("Message: " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"{nameof(message.Distribution)}:{message.Distribution} " +
                             $"forwarded to other node {nameof(message.SocketIdentity)}:{message.SocketIdentity.GetString()}");
            }
        }

        public void ReceivedFromOtherNode(Message message)
        {
            if (message.TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                var hops = string.Join("|",
                                       message
                                           .GetMessageHops()
                                           .Select(h => $"{nameof(h.Uri)}:{h.Uri.ToSocketAddress()}/{h.Identity.GetString()}"));

                logger.Trace("Message: " +
                             $"{nameof(message.Version)}:{message.Version.GetString()} " +
                             $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                             $"{nameof(message.Distribution)}:{message.Distribution} " +
                             $"received from other node via hops {hops}");
            }
        }
    }
}