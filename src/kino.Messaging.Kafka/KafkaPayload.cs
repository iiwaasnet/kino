using kino.Core.Framework;

namespace kino.Messaging.Kafka
{
    public abstract class KafkaPayload : Payload
    {
        protected new static byte[] BuildFullIdentity(string identity)
            => (Message.KinoMessageNamespace + ".KFK." + identity).GetBytes();
    }
}