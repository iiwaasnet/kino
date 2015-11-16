using kino.Core.Framework;
using kino.Core.Messaging;
using ProtoBuf;

namespace Server.Messages
{
    [ProtoContract]
    public class HelloMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "HELLO".GetBytes();

        [ProtoMember(1)]
        public string Greeting { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}