using kino.Connectivity.Kafka;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Routing.Kafka.ServiceMessageHandlers
{
    public class PingHandler : IKafkaServiceMessageHandler
    {
        public void Handle(IMessage message, ISender _)
        {
        }

        public MessageIdentifier TargetMessage => KinoMessages.Ping;
    }
}