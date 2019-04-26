using kino.Messaging.Messages;
using ProtoBuf;

namespace kino.Messaging.Kafka.Messages
{
    [ProtoContract]
    public class RegisterExternalKafkaMessageRouteMessage : KafkaPayload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REGEXTROUTE");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string BrokerUri { get; set; }

        [ProtoMember(2)]
        public byte[] NodeIdentity { get; set; }

        [ProtoMember(3)]
        public string Topic { get; set; }

        [ProtoMember(4)]
        public string Queue { get; set; }

        [ProtoMember(5)]
        public RouteRegistration[] Routes { get; set; }

        [ProtoMember(6)]
        public Health Health { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}