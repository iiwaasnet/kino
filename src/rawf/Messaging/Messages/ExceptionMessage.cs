using System;
using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class ExceptionMessage : IPayload
    {
        public static readonly byte[] MessageIdentity = "EXCEPTION".GetBytes();

        [ProtoMember(1)]
        public Exception Exception { get; set; }
    }
}