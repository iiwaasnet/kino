namespace kino.Client
{
    public interface ICallbackPoint
    {
        byte[] MessageIdentity { get; }
        byte[] MessageVersion { get; }
    }
}