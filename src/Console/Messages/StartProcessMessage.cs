namespace Console.Messages
{
    public class StartProcessMessage : TypedMessage<StartProcessMessage.Payload>
    {
        public const string MessageType = "STRPROC";

        public StartProcessMessage(Payload payload)
            : base(payload, MessageType)
        {
        }

        public class Payload
        {
            public string Arg { get; set; }
        }

        
    }
}