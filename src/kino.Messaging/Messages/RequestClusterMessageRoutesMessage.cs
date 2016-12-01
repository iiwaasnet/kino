using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestClusterMessageRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REQCLUSTROUTES");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string RequestorUri { get; set; }

        [ProtoMember(2)]
        public byte[] RequestorNodeIdentity { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}