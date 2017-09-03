using System.Collections.Generic;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class NodeExternalRegistration
    {
        [ProtoMember(1)]
        public byte[] NodeIdentity { get; set; }

        [ProtoMember(2)]
        public string NodeUri { get; set; }

        [ProtoMember(3)]
        public IEnumerable<byte[]> MessageHubs { get; set; }

        [ProtoMember(4)]
        public IEnumerable<MessageRegistration> MessageRoutes { get; set; }
    }    
}