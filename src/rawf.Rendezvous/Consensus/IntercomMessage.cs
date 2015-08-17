using rawf.Messaging;

namespace rawf.Rendezvous.Consensus
{
    internal class IntercomMessage
    {
        internal IMessage Message { get; set; }
        internal byte[] Receiver { get; set; }
    }
}