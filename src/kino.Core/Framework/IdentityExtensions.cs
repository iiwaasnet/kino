using kino.Core.Connectivity;

namespace kino.Core.Framework
{
    public static class IdentityExtensions
    {
        public static readonly byte[] Empty = new byte[0];

        public static bool IsSet(this byte[] buffer)
            => buffer != null && !Unsafe.Equals(buffer, Empty);

        public static bool IsMessageHub(this MessageIdentifier identity)
            => !identity.Version.IsSet();
    }
}