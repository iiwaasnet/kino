using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using ProtoBuf;

namespace kino.Connectivity
{
    [ProtoContract]
    public class UnregisterMessageRouteMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "UNREGMSGROUTE".GetBytes();

        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        [ProtoMember(3)]
        public MessageContract[] MessageContracts { get; set; }
    }
}