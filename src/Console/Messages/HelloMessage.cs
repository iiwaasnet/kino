namespace Console.Messages
{
    public class HelloMessage : IPayload
    {
        public const string MessageIdentity = "HELLO";

        public string Greeting { get; set; }
    }
}