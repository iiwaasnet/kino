﻿using System;
using ProtoBuf;

namespace kino.Messaging.Kafka.Messages
{
    [ProtoContract]
    public class Health
    {
        [ProtoMember(1)]
        public string Topic { get; set; }

        [ProtoMember(2)]
        public string GroupId { get; set; }

        [ProtoMember(3)]
        public TimeSpan HeartBeatInterval { get; set; }
    }
}