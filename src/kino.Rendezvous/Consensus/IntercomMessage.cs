using kino.Messaging;

namespace kino.Rendezvous.Consensus
{
    internal class IntercomMessage
    {
        internal IMessage Message { get; set; }
        internal byte[] Receiver { get; set; }
    }
}