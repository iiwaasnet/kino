namespace Console.Messages
{
    public class StartProcessMessage : IPayload
    {
        public const string MessageIdentity = "STRPROC";

        public string Arg { get; set; }
    }
}