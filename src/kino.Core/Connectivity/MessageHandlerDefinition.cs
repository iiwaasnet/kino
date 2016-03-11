namespace kino.Core.Connectivity
{
    public class MessageHandlerDefinition
    {
        public MessageHandler Handler { get; set; }

        public MessageDefinition Message { get; set; }

        public bool KeepRegistrationLocal { get; set; }
    }
}