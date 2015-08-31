using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class ProcessAnnouncementMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "ANNPROC".GetBytes();

        [ProtoMember(1)]
        public Node Node { get; set; }
    }
}