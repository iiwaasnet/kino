using System.Collections.Generic;
using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RendezvousConfigurationChangedMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "RNDZVRECONFIG".GetBytes();

        [ProtoMember(1)]
        public IEnumerable<RendezvousNode> RendezvousNodes { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}