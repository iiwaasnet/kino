namespace Console
{
    public class CallbackPoint : ICallbackPoint
    {
        public byte[] MessageIdentity { get; set; }
        public byte[] ReceiverIdentity { get; set; }
    }
}