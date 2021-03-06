﻿using kino.Messaging;
using ProtoBuf;

namespace kino.Consensus.Messages
{
    [ProtoContract]
    public class LeaseReadMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("READLEASE");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public Ballot Ballot { get; set; }

        [ProtoMember(2)]
        public byte[] SenderIdentity { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}