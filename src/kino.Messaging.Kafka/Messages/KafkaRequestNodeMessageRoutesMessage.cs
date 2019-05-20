using ProtoBuf;

namespace kino.Messaging.Kafka.Messages
{
    [ProtoContract]
    public class KafkaRequestNodeMessageRoutesMessage : KafkaPayload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REQNODEROUTES");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string TargetNodeBrokerName { get; set; }

        [ProtoMember(2)]
        public byte[] TargetNodeIdentity { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}