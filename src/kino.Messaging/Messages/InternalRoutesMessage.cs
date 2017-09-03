using System.Collections.Generic;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class InternalRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("INTROUTES");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public IEnumerable<ReceiverRegistration> MessageHubs { get; set; }

        [ProtoMember(2)]
        public IEnumerable<MessageRegistration> MessageRoutes { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}