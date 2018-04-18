using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class ReceiptConfirmationMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("RCPTCONFIRM");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        public static readonly ReceiptConfirmationMessage Instance = new ReceiptConfirmationMessage();

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}