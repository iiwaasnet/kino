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

        public static int GetInt(this byte[] array) 
            => BitConverter.ToInt32(array, 0);
    }
}