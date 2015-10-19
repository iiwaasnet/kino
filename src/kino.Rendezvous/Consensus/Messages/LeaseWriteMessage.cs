using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class LeaseWriteMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "WRITELEASE".GetBytes();

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }
        [ProtoMember(2)]
        public Lease Lease { get; set; }
        [ProtoMember(3)]
        public byte[] SenderIdentity { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}