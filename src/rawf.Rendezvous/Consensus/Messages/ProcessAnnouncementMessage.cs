using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class ProcessAnnouncementMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "ANNPROC".GetBytes();

        [ProtoMember(1)]
        public Node Node { get; set; }
    }
}