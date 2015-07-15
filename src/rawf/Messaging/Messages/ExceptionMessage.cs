using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class ExceptionMessage : IPayload
    {
        public static readonly byte[] MessageIdentity = "EXCEPTION".GetBytes();

        [ProtoMember(1)]
        public string Message { get; set; }

        [ProtoMember(2)]
        public string Source { get; set; }

        [ProtoMember(3)]
        public string StackTrace { get; set; }

        [ProtoMember(4)]
        public string InnerException { get; set; }
    }
}