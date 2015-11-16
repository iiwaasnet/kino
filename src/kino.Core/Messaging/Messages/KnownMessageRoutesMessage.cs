using System.Collections.Generic;
using kino.Core.Framework;
using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class KnownMessageRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "KNOWNMSGROUTES".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public MessageRoute InternalRoutes { get; set; }
        [ProtoMember(2)]
        public IEnumerable<MessageRoute> ExternalRoutes { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}