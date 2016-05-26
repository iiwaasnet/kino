using System;
using System.Text;

namespace kino.Core.Framework
{
    public static class DataEncoder
    {
        private static readonly Encoding Encoder;

        static DataEncoder()
        {
            Encoder = Encoding.UTF8;
        }

        public static ulong Combine(ushort v16, ushort v32)
            => v16
               | ((ulong) v32 << 16) & (0xFFFFul << 16);

        public static ulong Combine(ushort v16, ushort v32, ushort v48)
            => v16
               | ((ulong) v32 << 16) & (0xFFFFul << 16)
               | ((ulong) v48 << 32) & (0xFFFFul << 32);

        public static ulong Combine(ushort v16, ushort v32, ushort v48, ushort v64)
            => v16
               | ((ulong) v32 << 16) & (0xFFFFul << 16)
               | ((ulong) v48 << 32) & (0xFFFFul << 32)
               | ((ulong) v64 << 48) & (0xFFFFul << 48);

        public static void Split(this ulong data, out ushort v16)
            => v16 = (ushort) (data & 0xFFFF);

        public static void Split(this ulong data, out ushort v16, out ushort v32)
        {
            v16 = (ushort) (data & 0xFFFF);
            v32 = (ushort) ((data >> 16) & 0xFFFF);
        }

        public static void Split(this ulong data, out ushort v16, out ushort v32, out ushort v48)
        {
            v16 = (ushort) (data & 0xFFFF);
            v32 = (ushort) ((data >> 16) & 0xFFFF);
            v48 = (ushort) ((data >> 32) & 0xFFFF);
        }

        public static void Split(this ulong data, out ushort v16, out ushort v32, out ushort v48, out ushort v64)
        {
            v16 = (ushort) (data & 0xFFFF);
            v32 = (ushort) ((data >> 16) & 0xFFFF);
            v48 = (ushort) ((data >> 32) & 0xFFFF);
            v64 = (ushort) ((data >> 48) & 0xFFFF);
        }

        public static string GetAnyString(this byte[] array)
            => array.GetString();

        public static string GetString(this byte[] array)
            => Encoder.GetString(array);

        public static byte[] GetBytes(this string str)
            => Encoder.GetBytes(str);

        public static byte[] GetBytes(this int val)
            => BitConverter.GetBytes(val);

        public static byte[] GetBytes(this long val)
            => BitConverter.GetBytes(val);

        public static byte[] GetBytes(this ulong val)
            => BitConverter.GetBytes(val);

        public static byte[] GetBytes(this TimeSpan val)
            => BitConverter.GetBytes(val.Ticks);

        public static byte[] GetBytes(this DateTime utcDateTime)
            => BitConverter.GetBytes(utcDateTime.Ticks);

        public static int GetInt(this byte[] array)
            => BitConverter.ToInt32(array, 0);

        public static long GetLong(this byte[] array)
            => BitConverter.ToInt64(array, 0);

        public static ulong GetULong(this byte[] array)
            => BitConverter.ToUInt64(array, 0);

        public static TimeSpan GetTimeSpan(this byte[] array)
            => new TimeSpan(BitConverter.ToInt64(array, 0));

        public static DateTime GetUtcDateTime(this byte[] array)
            => new DateTime(BitConverter.ToInt64(array, 0), DateTimeKind.Utc);

        public static T GetEnumFromInt<T>(this byte[] array)
            where T : struct
        {
            var raw = array.GetInt();
            return CastToEnum<T, int>(raw);
        }

        public static T GetEnumFromLong<T>(this byte[] array)
            where T : struct
        {
            var raw = array.GetLong();
            return CastToEnum<T, long>(raw);
        }

        private static TEnum CastToEnum<TEnum, TRaw>(TRaw raw) where TEnum : struct
        {
            if (Enum.IsDefined(typeof(TEnum), raw))
            {
                return (TEnum) Enum.ToObject(typeof(TEnum), raw);
            }

            throw new InvalidCastException($"Unable to cast {raw} to enum {typeof(TEnum)}");
        }
    }
}