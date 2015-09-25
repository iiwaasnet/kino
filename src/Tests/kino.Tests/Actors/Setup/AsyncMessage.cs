using System;
using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Tests.Actors.Setup
{
    [ProtoContract]
    public class AsyncMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "ASYNCMSG".GetBytes();

        [ProtoMember(1)]
        public TimeSpan Delay { get; set; }
    }
}