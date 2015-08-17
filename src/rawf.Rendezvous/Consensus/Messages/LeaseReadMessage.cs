using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class LeaseReadMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "READLEASE".GetBytes();

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }
        [ProtoMember(2)]
        public byte[] SenderIdentity { get; set; }
    }
}