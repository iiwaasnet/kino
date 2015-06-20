using System;
using System.Text;

namespace Console.Messages
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

        public static int GetInt(this byte[] array)
            => BitConverter.ToInt32(array, 0);

        public static long GetLong(this byte[] array)
            => BitConverter.ToInt64(array, 0);

        public static TimeSpan GetTimeSpan(this byte[] array)
            => new TimeSpan(BitConverter.ToInt64(array, 0));

        public static T GetEnum<T>(this byte[] array)
            where T : struct
        {
            var raw = array.GetInt();
            if (Enum.IsDefined(typeof (T), raw))
            {
                return (T) Enum.ToObject(typeof (T), raw);
            }

            throw new InvalidCastException($"Unable to cast {raw} to enum {typeof (T)}");
        }
    }
}