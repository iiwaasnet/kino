using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace Server.Messages
{
    [ProtoContract]
    public class GroupCharsRequestMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "GRPCHARSREQ".GetBytes();

        [ProtoMember(1)]
        public string Text { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}