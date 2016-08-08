namespace kino.Core.Security
{
    public interface ISignatureProvider
    {
        byte[] CreateSignature(string domain, byte[] buffer);
    }
}