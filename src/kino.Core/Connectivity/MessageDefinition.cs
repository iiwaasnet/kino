using kino.Core.Framework;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class MessageDefinition
    {
        public MessageDefinition(byte[] identity, byte[] version, byte[] partition)
        {
            Identity = identity;
            Version = version;
            Partition = partition;
        }

        public static MessageDefinition Create<T>(byte[] partition)
            where T : IMessageIdentifier, new()
        {
            var message = new T();
            return new MessageDefinition(message.Identity, message.Version, partition);
        }

        public static MessageDefinition Create<T>()
            where T : IMessageIdentifier, new()
            => Create<T>(IdentityExtensions.Empty);

        public byte[] Identity { get; }

        public byte[] Version { get; }

        public byte[] Partition { get; }
    }
}