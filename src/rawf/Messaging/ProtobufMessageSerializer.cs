using System;
using System.IO;
using ProtoBuf;
using ProtoBuf.Meta;

namespace rawf.Messaging
{
    public class ProtobufMessageSerializer : IMessageSerializer
    {
        static ProtobufMessageSerializer()
        {
            RuntimeTypeModel.Default.Add(typeof (Exception), true)
                            .AddSubType(1, typeof (AggregateException));
            RuntimeTypeModel.Default.Add(typeof (AggregateException), true);
        }

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