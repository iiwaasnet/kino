namespace Console.Messages
{
    public class EhlloMessage : IPayload
    {
        public const string MessageIdentity = "EHHLO";

        public string Ehllo { get; set; }
    }
}