namespace kino.Actors
{
    public class MessageHandlerDefinition
    {
        public MessageHandler Handler { get; set; }

        public MessageDefinition Message { get; set; }

        public bool KeepRegistrationLocal { get; set; }
    }
}