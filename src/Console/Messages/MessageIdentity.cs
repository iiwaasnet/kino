namespace Console.Messages
{
    public class MessageIdentity
    {
        public byte[] Version { get; set; }
        public byte[] Identity { get; set; }
        public byte[] ReceiverIdentity { get; set; }
    }
}