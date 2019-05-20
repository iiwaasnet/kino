using kino.Messaging.Messages;
using ProtoBuf;

namespace kino.Messaging.Kafka.Messages
{
    [ProtoContract]
    public class KafkaDiscoverMessageRouteMessage : KafkaPayload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("DISCOVMSGROUTE");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public byte[] RequestorNodeIdentity { get; set; }

        [ProtoMember(2)]
        public string RequestorBrokerName { get; set; }

        [ProtoMember(3)]
        public byte[] ReceiverIdentity { get; set; }

        [ProtoMember(4)]
        public MessageContract MessageContract { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}