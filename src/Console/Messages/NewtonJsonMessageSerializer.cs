using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Console.Messages
{
    public class NewtonJsonMessageSerializer : IMessageSerializer
    {
        public byte[] Serialize(object obj)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BsonWriter(stream))
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
                using (var reader = new BsonReader(stream))
                {
                    return JsonSerializer
                        .Create()
                        .Deserialize<T>(reader);
                }
            }
        }
    }
}