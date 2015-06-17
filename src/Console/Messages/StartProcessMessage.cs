namespace Console.Messages
{
    public class StartProcessMessage : TypedMessage<StartProcessMessage.Payload>
    {
        public const string MessageIdentity = "STRPROC";

        public StartProcessMessage(Payload payload)
            : base(payload, MessageIdentity)
        {
        }

        public class Payload
        {
            public string Arg { get; set; }
        }

        
    }
}