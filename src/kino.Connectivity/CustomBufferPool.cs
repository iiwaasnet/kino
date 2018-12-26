using System.Buffers;
using NetMQ;

namespace kino.Connectivity
{
#if !NET47
    public class CustomBufferPool : IBufferPool
    {
        public byte[] Take(int size)
            => ArrayPool<byte>.Shared.Rent(size);

        public void Return(byte[] buffer)
            => ArrayPool<byte>.Shared.Return(buffer, true);

        public void Dispose()
        {
        }
    }
#endif
}