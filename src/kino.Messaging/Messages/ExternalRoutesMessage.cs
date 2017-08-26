using System.Collections.Generic;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class ExternalRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("EXTROUTES");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public IEnumerable<NodeExternalRegistration> Routes { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}