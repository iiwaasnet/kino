using kino.Core.Framework;
using kino.Core.Messaging;
using ProtoBuf;

namespace kino.Tests.Actors.Setup
{
    [ProtoContract]
    public class SimpleMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "SIMPLE".GetBytes();

        [ProtoMember(1)]
        public string Content { get; set; }

        public override ushort Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}