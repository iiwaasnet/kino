using System.Buffers;
using NetMQ;

namespace kino.Connectivity
{
    public class CustomBufferPool : IBufferPool
    {
        public void Dispose()
        {
        }

        public byte[] Take(int size)
            => ArrayPool<byte>.Shared.Rent(size);

        public void Return(byte[] buffer)
            => ArrayPool<byte>.Shared.Return(buffer, true);
    }
}