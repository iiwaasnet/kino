using System;
using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Tests.Backend.Setup
{
    [ProtoContract]
    public class AsyncExceptionMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "ASYNCEXCMSG".GetBytes();

        [ProtoMember(1)]
        public TimeSpan Delay { get; set; }

        [ProtoMember(2)]
        public string ErrorMessage { get; set; }
    }
}