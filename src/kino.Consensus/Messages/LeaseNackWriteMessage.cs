using kino.Core.Framework;
using kino.Core.Messaging;
using ProtoBuf;

namespace kino.Consensus.Messages
{
    [ProtoContract]
    public class LeaseNackWriteMessage : Payload, ILeaseMessage
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("NACKWRITELEASE");
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }

        [ProtoMember(2)]
        public string SenderUri { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}