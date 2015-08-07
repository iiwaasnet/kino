using System;
using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class PongMessage : Payload
    {        
        public static readonly byte[] MessageIdentity = "PONG".GetBytes();
        
        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }
    }
}