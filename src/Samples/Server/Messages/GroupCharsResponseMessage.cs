using System.Collections.Generic;
using kino.Core.Framework;
using kino.Messaging;
using ProtoBuf;

namespace Server.Messages
{
    [ProtoContract]
    public class GroupCharsResponseMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "GRPCHARSRESP".GetBytes();

        [ProtoMember(1)]
        public IEnumerable<GroupInfo> Groups { get; set; }

        [ProtoMember(2)]
        public string Text { get; set; }

        public override ushort Version => Message.CurrentVersion;

        public override byte[] Identity => MessageIdentity;
    }

    [ProtoContract]
    public class GroupInfo
    {
        [ProtoMember(1)]
        public char Char { get; set; }

        [ProtoMember(2)]
        public int Count { get; set; }
    }
}