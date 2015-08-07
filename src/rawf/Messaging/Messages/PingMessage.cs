using System;
using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class PingMessage : Payload
    {        
        public static readonly byte[] MessageIdentity = "PING".GetBytes();
    }
}