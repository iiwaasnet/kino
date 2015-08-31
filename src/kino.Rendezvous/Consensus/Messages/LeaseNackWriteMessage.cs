using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class LeaseNackWriteMessage : Payload, ILeaseMessage
    {
        public static readonly byte[] MessageIdentity = "NACKWRITELEASE".GetBytes();

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }

        [ProtoMember(2)]
        public string SenderUri { get; set; }
    }
}