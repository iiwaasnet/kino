namespace kino.Messaging.Kafka
{
    public class MessageIdentifier
    {
        public byte[] Identity { get; set; }

        public ushort Version { get; set; }

        public byte[] Partition { get; set; }
    }
}