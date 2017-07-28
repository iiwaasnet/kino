using kino.Messaging;
using ProtoBuf;

namespace kino.Consensus.Messages
{
    [ProtoContract]
    public class ReconnectClusterMemberMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("QUORUM-RECONNECT");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string NodeUri { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}