using System;
using kino.Core.Framework;
using kino.Core.Messaging;
using ProtoBuf;

namespace kino.Tests.Actors.Setup
{
    [ProtoContract]
    public class AsyncMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "ASYNCMSG".GetBytes();

        [ProtoMember(1)]
        public TimeSpan Delay { get; set; }

        public override ushort Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}