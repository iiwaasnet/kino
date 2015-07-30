﻿using ProtoBuf;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class MessageHandlerRegistration
    {
        [ProtoMember(1)]
        public byte[] Version { get; set; }

        [ProtoMember(2)]
        public byte[] Identity { get; set; }
    }
}