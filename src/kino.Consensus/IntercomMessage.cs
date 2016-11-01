using kino.Messaging;

namespace kino.Consensus
{
    internal class IntercomMessage
    {
        internal IMessage Message { get; set; }

        internal byte[] Receiver { get; set; }
    }
}