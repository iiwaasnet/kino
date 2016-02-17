using kino.Core.Framework;
using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class RequestNodeMessageRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REQNODEROUTES");
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string TargetNodeUri { get; set; }

        [ProtoMember(2)]
        public byte[] TargetNodeIdentity { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}