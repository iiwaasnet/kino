using System.Linq;
using kino.Core.Framework;
using kino.Messaging;

namespace kino.Cluster
{
    public partial class ScaleOutListener
    {
        private void ReceivedFromOtherNode(Message message)
        {
            if ((message.TraceOptions & MessageTraceOptions.Routing) == MessageTraceOptions.Routing)
            {
                var hops = string.Join("|",
                                       message.GetMessageRouting()
                                              .Select(h => $"{nameof(h.Uri)}:{h.Uri}/{h.Identity.GetAnyString()}"));

                logger.Trace($"Message: {message} received from other node via hops {hops}");
            }
        }
    }
}