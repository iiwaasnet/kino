using System;
using System.Text;

namespace kino.Core.Framework
{
    public static class DataEncoder
    {
        private static readonly Encoding encoder;

        static DataEncoder()
        {
            encoder = Encoding.UTF8;
        }

        public static string GetString(this byte[] array)
            => encoder.GetString(array);

        public static byte[] GetBytes(this string str)
            => encoder.GetBytes(str);

        public static byte[] GetBytes(this int val)
            => BitConverter.GetBytes(val);

        public static byte[] GetBytes(this long val)
            => BitConverter.GetBytes(val);

        public static byte[] GetBytes(this TimeSpan val)
            => BitConverter.GetBytes(val.Ticks);

        public static byte[] GetBytes(this DateTime utcDateTime)
            => BitConverter.GetBytes(utcDateTime.Ticks);

        public static int GetInt(this byte[] array)
            => BitConverter.ToInt32(array, 0);

        public static long GetLong(this byte[] array)
            => BitConverter.ToInt64(array, 0);

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
            if (Enum.IsDefined(typeof (TEnum), raw))
            {
                return (TEnum) Enum.ToObject(typeof (TEnum), raw);
            }

            throw new InvalidCastException($"Unable to cast {raw} to enum {typeof (TEnum)}");
        }
    }
}