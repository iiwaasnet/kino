using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RendezvousNotLeaderMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "RNDZVNOTLEADER".GetBytes();

        [ProtoMember(1)]
        public string LeaderMulticastUri { get; set; }

        [ProtoMember(2)]
        public string LeaderUnicastUri { get; set; }
    }
}