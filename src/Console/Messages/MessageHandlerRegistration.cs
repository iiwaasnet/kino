namespace Console.Messages
{
    public class MessageHandlerRegistration
    {
        public byte[] Version { get; set; }
        public byte[] Identity { get; set; }
        public IdentityType IdentityType { get; set; }
    }

    public enum IdentityType
    {
        Actor,
        Callback
    }
}