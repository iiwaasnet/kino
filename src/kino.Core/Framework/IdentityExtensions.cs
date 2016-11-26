namespace kino.Core.Framework
{
    public static class IdentityExtensions
    {
        public static readonly byte[] Empty = new byte[0];

        public static bool IsSet(this byte[] buffer)
            => buffer != null && !Unsafe.ArraysEqual(buffer, Empty);

        public static bool IsSet(this ReceiverIdentifier identitifier)
            => identitifier?.Identity != null && !Unsafe.ArraysEqual(identitifier.Identity, Empty);

        public static bool IsMessageHub(this Identifier identity)
            => identity is AnyIdentifier;
    }
}