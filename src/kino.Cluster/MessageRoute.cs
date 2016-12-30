using kino.Core;

namespace kino.Cluster
{
    public class MessageRoute
    {
        public ReceiverIdentifier Receiver { get; set; }

        public MessageIdentifier Message { get; set; }
    }
}