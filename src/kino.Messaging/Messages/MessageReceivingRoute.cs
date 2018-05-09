using System.Collections.Generic;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class MessageReceivingRoute
    {
        [ProtoMember(1)]
        public byte[] NodeIdentity { get; set; }

        [ProtoMember(2)]
        public IEnumerable<byte[]> ReceiverIdentity { get; set; }
    }
}