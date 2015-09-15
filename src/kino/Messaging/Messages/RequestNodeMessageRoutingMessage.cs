using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestNodeMessageRoutingMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REQNODEROUTE".GetBytes();

        [ProtoMember(1)]
        public string TargetNodeUri { get; set; }

        [ProtoMember(2)]
        public byte[] TargetNodeIdentity { get; set; }                
    }
}