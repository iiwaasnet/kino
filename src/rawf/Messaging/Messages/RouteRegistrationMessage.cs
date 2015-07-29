﻿using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class RouteRegistrationMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "ROUTEREG".GetBytes();

        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        [ProtoMember(3)]
        public MessageRegistration[] Registrations { get; set; }
    }
}