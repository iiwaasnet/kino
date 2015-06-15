using System.IO;
using ServiceStack.Text;

namespace Console.Messages
{
    public class MessageSerializer : IMessageSerializer
    {
        public byte[] Serialize(object obj)
        {
            using (var stream = new MemoryStream())
            {
                JsonSerializer.SerializeToStream(obj, stream);

                return stream.GetBuffer();
            }
        }

        public T Deserialize<T>(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            {
                return JsonSerializer.DeserializeFromStream<T>(stream);
            }
        }
    }
}