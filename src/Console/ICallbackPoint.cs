namespace Console
{
    public interface ICallbackPoint
    {
        byte[] MessageIdentity { get; }
        byte[] ReceiverIdentity { get; }
    }
}