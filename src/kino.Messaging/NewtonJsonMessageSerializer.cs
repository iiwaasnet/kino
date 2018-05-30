using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace kino.Messaging
{
    public class NewtonJsonMessageSerializer : IMessageSerializer
    {
        public byte[] Serialize(object obj)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BsonDataWriter(stream))
                {
                    JsonSerializer
                       .Create()
                       .Serialize(writer, obj);

                    writer.Flush();

                    return stream.GetBuffer();
                }
            }
        }

        public T Deserialize<T>(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            {
                using (var reader = new BsonDataReader(stream))
                {
                    return JsonSerializer
                          .Create()
                          .Deserialize<T>(reader);
                }
            }
        }
    }
}