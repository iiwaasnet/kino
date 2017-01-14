using System;

namespace kino.Tests.Helpers
{
    public static class Randomizer
    {
        private static readonly Random rnd;

        static Randomizer()
        {
            rnd = new Random((int) (0x0000ffff & DateTime.UtcNow.Ticks));
        }

        public static int Int32()
            => rnd.Next();

        public static long Int64()
            => rnd.Next();

        public static int Int32(int min, int max)
            => rnd.Next(min, max);

        public static ushort UInt16()
            => (ushort) rnd.Next();

        public static ushort UInt16(int min, int max)
            => (ushort) rnd.Next(min, max);
    }
}