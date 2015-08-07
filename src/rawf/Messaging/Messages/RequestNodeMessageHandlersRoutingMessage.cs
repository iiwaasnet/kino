using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class RequestNodeMessageHandlersRoutingMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REQNODEROUTE".GetBytes();

        [ProtoMember(1)]
        public string TargetNodeUri { get; set; }

        [ProtoMember(2)]
        public byte[] TargetNodeIdentity { get; set; }                
    }
}