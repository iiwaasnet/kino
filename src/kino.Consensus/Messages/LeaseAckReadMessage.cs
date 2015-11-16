using kino.Core.Framework;
using kino.Core.Messaging;
using ProtoBuf;

namespace kino.Consensus.Messages
{
    [ProtoContract]
    public class LeaseAckReadMessage : Payload, ILeaseMessage
    {
        private static readonly byte[] MessageIdentity = "ACKREADLEASE".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }

        [ProtoMember(2)]
        public Ballot KnownWriteBallot { get; set; }

        [ProtoMember(3)]
        public Lease Lease { get; set; }

        [ProtoMember(4)]
        public string SenderUri { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}