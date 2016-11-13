using kino.Core.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Tests.Actors.Setup
{
    [ProtoContract]
    public class LocalMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "LOCAL".GetBytes();

        [ProtoMember(1)]
        public string Content { get; set; }

        public override ushort Version => Message.CurrentVersion;

        public override byte[] Identity => MessageIdentity;
    }
}