using System.Collections.Generic;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class MessageRegistration
    {
        [ProtoMember(1)]
        public MessageContract Message { get; set; }

        [ProtoMember(2)]
        public IEnumerable<byte[]> Actors { get; set; }
    }
}