using System;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class ExceptionMessage : Payload
    {
        private static readonly IMessageSerializer MessageSerializer = new NewtonJsonMessageSerializer();

        private static readonly byte[] MessageIdentity = BuildFullIdentity("EXCEPTION");

        public ExceptionMessage()
            : base(MessageSerializer)
        {
        }

        [ProtoMember(1)]
        public Exception Exception { get; set; }

        [ProtoMember(2)]
        public string StackTrace { get; set; }

        public override ushort Version => Message.CurrentVersion;

        public override byte[] Identity => MessageIdentity;
    }
}