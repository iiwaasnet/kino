namespace kino.Client
{
    public class CallbackHandlerKey
    {
        public byte[] Version { get; set; }

        public byte[] Identity { get; set; }

        public byte[] Partition { get; set; }

        public byte[] Correlation { get; set; }
    }
}