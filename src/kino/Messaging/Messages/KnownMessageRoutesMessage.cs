using System.Collections.Generic;
using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class KnownMessageRoutesMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "KNOWNMSGROUTES".GetBytes();

        [ProtoMember(1)]
        public MessageRoute InternalRoutes { get; set; }
        [ProtoMember(2)]
        public IEnumerable<MessageRoute> ExternalRoutes { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }

    [ProtoContract]
    public class MessageRoute
    {
        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        [ProtoMember(3)]
        public MessageContract[] MessageContracts { get; set; }
    }   
}