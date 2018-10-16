using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace kino.Core.Framework
{
    public static class DataEncoder
    {
        private static readonly Encoding Encoder;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static DataEncoder()
            => Encoder = Encoding.UTF8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Combine(ushort v16, ushort v32)
            => v16
             | ((ulong) v32 << 16) & (0xFFFFul << 16);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Combine(ushort v16, ushort v32, ushort v48)
            => v16
             | ((ulong) v32 << 16) & (0xFFFFul << 16)
             | ((ulong) v48 << 32) & (0xFFFFul << 32);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Combine(ushort v16, ushort v32, ushort v48, ushort v64)
            => v16
             | ((ulong) v32 << 16) & (0xFFFFul << 16)
             | ((ulong) v48 << 32) & (0xFFFFul << 32)
             | ((ulong) v64 << 48) & (0xFFFFul << 48);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Split16(this ulong data)
            => (ushort) (data & 0xFFFF);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ushort v16, ushort v32) Split32(this ulong data)
            => (v16: (ushort) (data & 0xFFFF),
                   v32: (ushort) ((data >> 16) & 0xFFFF));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ushort v16, ushort v32, ushort v48) Split48(this ulong data)
            => (v16: (ushort) (data & 0xFFFF),
                   v32: (ushort) ((data >> 16) & 0xFFFF),
                   v48: (ushort) ((data >> 32) & 0xFFFF));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ushort v16, ushort v32, ushort v48, ushort v64) Split64(this ulong data)
            => (v16: (ushort) (data & 0xFFFF),
                   v32: (ushort) ((data >> 16) & 0xFFFF),
                   v48: (ushort) ((data >> 32) & 0xFFFF),
                   v64: (ushort) ((data >> 48) & 0xFFFF));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetAnyString(this byte[] array)
            => array.GetString();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetString(this byte[] array)
            => Encoder.GetString(array);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetString(this Span<byte> array)
            => Encoder.GetString(array);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetBytes(this string str)
            => Encoder.GetBytes(str);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetBytes(this ushort val)
            => BitConverter.GetBytes(val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetBytes(this int val)
            => BitConverter.GetBytes(val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetBytes(this long val)
            => BitConverter.GetBytes(val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetBytes(this ulong val)
            => BitConverter.GetBytes(val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetBytes(this TimeSpan val)
            => BitConverter.GetBytes(val.Ticks);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetBytes(this DateTime utcDateTime)
            => BitConverter.GetBytes(utcDateTime.Ticks);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> GetInt(this Span<byte> array, out int val)
        {
            val = BitConverter.ToInt32(array);
            return array.Slice(sizeof(int));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetInt(this byte[] array)
            => BitConverter.ToInt32(array, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetLong(this byte[] array)
            => BitConverter.ToInt64(array, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> GetLong(this Span<byte> array, out long val)
        {
            val = BitConverter.ToInt64(array);

            return array.Slice(sizeof(long));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetULong(this byte[] array)
            => BitConverter.ToUInt64(array, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> GetULong(this Span<byte> array, out ulong val)
        {
            val = BitConverter.ToUInt64(array);
            return array.Slice(sizeof(ulong));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort GetUShort(this byte[] array)
            => BitConverter.ToUInt16(array, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> GetUShort(this Span<byte> array, out ushort val)
        {
            val = BitConverter.ToUInt16(array);
            return array.Slice(sizeof(ushort));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan GetTimeSpan(this byte[] array)
            => new TimeSpan(BitConverter.ToInt64(array, 0));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> GetTimeSpan(this Span<byte> array, out TimeSpan val)
        {
            var slice = array.GetLong(out var int64);
            val = new TimeSpan(int64);

            return slice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime GetUtcDateTime(this byte[] array)
            => new DateTime(BitConverter.ToInt64(array, 0), DateTimeKind.Utc);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetEnumFromInt<T>(this byte[] array)
            where T : struct
        {
            var raw = array.GetInt();
            return CastToEnum<T, int>(raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetEnumFromLong<T>(this byte[] array)
            where T : struct
        {
            var raw = array.GetLong();
            return CastToEnum<T, long>(raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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