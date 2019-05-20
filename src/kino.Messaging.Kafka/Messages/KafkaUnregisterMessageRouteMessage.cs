using kino.Messaging.Messages;
using ProtoBuf;

namespace kino.Messaging.Kafka.Messages
{
    [ProtoContract]
    public class KafkaUnregisterMessageRouteMessage : KafkaPayload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("UNREGMSGROUTE");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string BrokerName { get; set; }

        [ProtoMember(2)]
        public byte[] ReceiverNodeIdentity { get; set; }

        [ProtoMember(3)]
        public RouteRegistration[] Routes { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}