namespace kino.Security
{
    public interface ISignatureProvider
    {
        byte[] CreateSignature(string domain, byte[] buffer);

        bool ShouldSignMessage(string domain, byte[] identity);
    }
}