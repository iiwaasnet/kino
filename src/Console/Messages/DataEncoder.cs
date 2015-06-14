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
        {
            return encoder.GetString(array);
        }

        public static byte[] GetBytes(this string str)
        {
            return encoder.GetBytes(str);
        }

        public static byte[] GetBytes(this int val)
        {
            return BitConverter.GetBytes(val);
        }

        public static int GetInt(this byte[] array)
        {
            return BitConverter.ToInt32(array, 0);
        }
    }
}