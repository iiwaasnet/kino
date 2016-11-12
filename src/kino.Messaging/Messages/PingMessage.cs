using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class PingMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("PING");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}