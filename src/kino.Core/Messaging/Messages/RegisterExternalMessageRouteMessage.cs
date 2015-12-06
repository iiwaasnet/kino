﻿using kino.Core.Framework;
using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class RegisterExternalMessageRouteMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "REGEXTROUTE".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        [ProtoMember(3)]
        public MessageContract[] MessageContracts { get; set; }

        public override byte[] Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}