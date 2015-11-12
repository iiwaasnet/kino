using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Consensus.Messages
{
    [ProtoContract]
    public class LeaseWriteMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "WRITELEASE".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }
        [ProtoMember(2)]
        public Lease Lease { get; set; }
        [ProtoMember(3)]
        public byte[] SenderIdentity { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}