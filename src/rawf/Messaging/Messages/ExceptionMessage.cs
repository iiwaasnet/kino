using System;
using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class ExceptionMessage : Payload
    {
        private static readonly IMessageSerializer messageSerializer = new NewtonJsonMessageSerializer();
        public static readonly byte[] MessageIdentity = "EXCEPTION".GetBytes();

        public ExceptionMessage()
            :base(messageSerializer)
        {
        }

        [ProtoMember(1)]
        public Exception Exception { get; set; }
    }
}