namespace kino.Security
{
    public interface ISignatureProvider
    {
        byte[] CreateSignature(string domain, byte[] buffer);

        bool SigningEnabled();
    }
}