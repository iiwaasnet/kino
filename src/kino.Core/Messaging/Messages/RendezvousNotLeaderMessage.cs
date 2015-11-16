using kino.Core.Framework;
using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class RendezvousNotLeaderMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "RNDZVNOTLEADER".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public RendezvousNode NewLeader { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}