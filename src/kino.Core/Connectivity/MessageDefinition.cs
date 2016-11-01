using kino.Core.Framework;

namespace kino.Core.Connectivity
{
    public class MessageDefinition
    {
        public MessageDefinition(byte[] identity, ushort version, byte[] partition)
        {
            Identity = identity;
            Version = version;
            Partition = partition;
        }

        public static MessageDefinition Create<T>(byte[] partition)
            where T : IIdentifier, new()
        {
            var message = new T();
            return new MessageDefinition(message.Identity, message.Version, partition);
        }

        public static MessageDefinition Create<T>()
            where T : IIdentifier, new()
        => Create<T>(IdentityExtensions.Empty);

        public byte[] Identity { get; }

        public ushort Version { get; }

        public byte[] Partition { get; }
    }
}