namespace kino.Core.Framework
{
    public static class IdentityExtensions
    {
        public static readonly byte[] Empty = new byte[0];

        public static bool IsSet(this byte[] buffer)
            => buffer != null && !Unsafe.ArraysEqual(buffer, Empty);
    }
}