using System.IO;
using ProtoBuf;

namespace kino.Messaging
{
    public class ProtobufMessageSerializer : IMessageSerializer
    {
        public byte[] Serialize(object obj)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, obj);
                stream.Flush();

                return stream.ToArray();
            }
        }

        public T Deserialize<T>(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            {
                return Serializer.Deserialize<T>(stream);
            }
        }
    }
}