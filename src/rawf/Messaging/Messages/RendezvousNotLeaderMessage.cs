using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class RendezvousNotLeaderMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "RNDZNOTLEADER".GetBytes();

        [ProtoMember(1)]
        public string LeaderMulticastUri { get; set; }

        [ProtoMember(2)]
        public string LeaderUnicastUri { get; set; }
    }
}