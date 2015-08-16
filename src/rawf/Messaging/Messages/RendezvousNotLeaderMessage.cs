using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class RendezvousNotLeaderMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "RNDZNOTLEADER".GetBytes();

        [ProtoMember(1)]
        public string LeaderUri { get; set; }
    }
}