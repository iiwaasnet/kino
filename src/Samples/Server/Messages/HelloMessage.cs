using kino.Core.Framework;
using kino.Messaging;
using ProtoBuf;

namespace Server.Messages
{
    [ProtoContract]
    public class HelloMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "HELLO".GetBytes();

        [ProtoMember(1)]
        public string Greeting { get; set; }

        public override ushort Version => Message.CurrentVersion;

        public override byte[] Identity => MessageIdentity;
    }
}