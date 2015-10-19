using System;
using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class ExceptionMessage : Payload
    {
        private static readonly IMessageSerializer messageSerializer = new NewtonJsonMessageSerializer();
        public static readonly byte[] MessageIdentity = "EXCEPTION".GetBytes();
        public static readonly byte[] MessageVersion = Message.CurrentVersion;

        public ExceptionMessage()
            :base(messageSerializer)
        {
        }

        [ProtoMember(1)]
        public Exception Exception { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}