using kino.Core.Framework;
using kino.Core.Messaging;
using ProtoBuf;

namespace kino.Tests.Actors.Setup
{
    [ProtoContract]
    public class NullMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "NULL".GetBytes();

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}