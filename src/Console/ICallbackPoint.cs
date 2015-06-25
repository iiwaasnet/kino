namespace Console
{
    public interface ICallbackPoint
    {
        string MessageIdentity { get; set; }
        string ReceiverIdentity { get; set; }
    }
}