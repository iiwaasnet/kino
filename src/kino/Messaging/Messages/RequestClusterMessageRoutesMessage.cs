using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestClusterMessageRoutesMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REQCLUSTROUTES".GetBytes();

        [ProtoMember(1)]
        public string RequestorUri { get; set; }

        [ProtoMember(2)]
        public byte[] RequestorSocketIdentity { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}