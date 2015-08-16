using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Rendezvous.Messages
{
    [ProtoContract]
    public class RendezvousNotLeaderMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "RNDZNOTLEADER".GetBytes();

        [ProtoMember(1)]
        public string LeaderUri { get; set; }
    }
}