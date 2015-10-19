using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class LeaseAckReadMessage : Payload, ILeaseMessage
    {
        public static readonly byte[] MessageIdentity = "ACKREADLEASE".GetBytes();

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }
        [ProtoMember(2)]
        public Ballot KnownWriteBallot { get; set; }
        [ProtoMember(3)]
        public Lease Lease { get; set; }
        [ProtoMember(4)]
        public string SenderUri { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}