using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class CheckStalePeersMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("CHCKSTALEPEERS");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}