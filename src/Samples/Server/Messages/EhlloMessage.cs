using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace Server.Messages
{
    [ProtoContract]
    public class EhlloMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "EHHLO".GetBytes();

        [ProtoMember(1)]
        public string Ehllo { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}