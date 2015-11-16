namespace kino.Core.Connectivity
{
    public class MessageDefinition
    {
        public MessageDefinition(byte[] identity, byte[] version)
        {
            Identity = identity;
            Version = version;
        }

        public static MessageDefinition Create<T>()
            where T: MessageIdentifier, new()
        {
            var message = new T();
            return new MessageDefinition(message.Identity, message.Version);
        }
    
        public byte[] Identity { get; }
        public byte[] Version { get; }
    }
}