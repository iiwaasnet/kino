using System;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class Health
    {
        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public TimeSpan HeartBeatInterval { get; set; }
    }
}