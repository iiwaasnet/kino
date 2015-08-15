using rawf.Messaging;

namespace rawf.Consensus
{
    internal class IntercomMessage
    {
        internal IMessage Message { get; set; }
        internal byte[] Receiver { get; set; }
    }
}