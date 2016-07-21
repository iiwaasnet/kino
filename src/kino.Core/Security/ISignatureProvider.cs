namespace kino.Core.Security
{
    public interface ISignatureProvider
    {
        byte[] CreateSignature(string securityDomain, byte[] buffer);
    }
}