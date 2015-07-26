using System;
using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Tests.Backend.Setup
{
    [ProtoContract]
    public class AsyncMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "ASYNCMSG".GetBytes();

        [ProtoMember(1)]
        public TimeSpan Delay { get; set; }
    }
}