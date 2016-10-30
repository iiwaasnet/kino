using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class RendezvousNotLeaderMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("RNDZVNOTLEADER");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public RendezvousNode NewLeader { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}