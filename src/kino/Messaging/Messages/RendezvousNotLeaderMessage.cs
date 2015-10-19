using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RendezvousNotLeaderMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "RNDZVNOTLEADER".GetBytes();

        [ProtoMember(1)]
        public RendezvousNode NewLeader { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}