using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class LeaseReadMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "READLEASE".GetBytes();

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }
        [ProtoMember(2)]
        public byte[] SenderIdentity { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}