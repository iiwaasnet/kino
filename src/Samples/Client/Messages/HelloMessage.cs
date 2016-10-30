using kino.Core.Framework;
using kino.Core.Messaging;
using ProtoBuf;

namespace Client.Messages
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