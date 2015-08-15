using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Consensus.Messages
{
    [ProtoContract]
    public class LeaseNackWriteMessage : Payload, ILeaseMessage
    {
        public static readonly byte[] MessageIdentity = "NACKWRITELEASE".GetBytes();

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }

        [ProtoMember(2)]
        public string Uri { get; set; }
    }
}