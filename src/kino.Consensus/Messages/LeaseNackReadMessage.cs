using kino.Core.Messaging;
using ProtoBuf;

namespace kino.Consensus.Messages
{
    [ProtoContract]
    public class LeaseNackReadMessage : Payload, ILeaseMessage
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("NACKREADLEASE");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }

        [ProtoMember(2)]
        public string SenderUri { get; set; }

        public override ushort Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}