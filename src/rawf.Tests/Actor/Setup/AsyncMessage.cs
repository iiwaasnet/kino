using System;
using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Tests.Actor.Setup
{
    [ProtoContract]
    public class AsyncMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "ASYNCMASG".GetBytes();

        [ProtoMember(1)]
        public TimeSpan Delay { get; set; }
    }
}