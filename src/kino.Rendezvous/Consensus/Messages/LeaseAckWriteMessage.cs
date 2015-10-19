using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class LeaseAckWriteMessage : Payload, ILeaseMessage
    {
        public static readonly byte[] MessageIdentity = "ACKWRITELEASE".GetBytes();

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }
        [ProtoMember(2)]
        public string SenderUri { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}