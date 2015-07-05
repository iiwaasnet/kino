using System;
using ProtoBuf;

namespace rawf.Messaging.Messages
{
    public class ExceptionMessage : IPayload
    {
        public const string MessageIdentity = "EXCEPTION";

        [ProtoMember(1)]
        public Exception Exception { get; set; }
    }
}