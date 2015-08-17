using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class LeaseAckWriteMessage : Payload, ILeaseMessage
    {
        public static readonly byte[] MessageIdentity = "ACKWRITELEASE".GetBytes();

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }
        [ProtoMember(2)]
        public string SenderUri { get; set; }
    }
}