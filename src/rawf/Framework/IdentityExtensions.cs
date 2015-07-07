namespace rawf.Framework
{
    public static class IdentityExtensions
    {
        private static readonly byte[] Empty = new byte[0];

        public static bool IsSet(this byte[] buffer)
        {
            return !Unsafe.Equals(buffer, Empty);
        }
    }
}