using System;
using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Tests.Actors.Setup
{
    [ProtoContract]
    public class AsyncMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "ASYNCMSG".GetBytes();

        [ProtoMember(1)]
        public TimeSpan Delay { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}