using ProtoBuf;

namespace rawf.Messaging
{
    [ProtoContract]
    public class MessageHandlerRegistration
    {
        [ProtoMember(1)]
        public byte[] Version { get; set; }

        [ProtoMember(2)]
        public byte[] Identity { get; set; }

        [ProtoMember(3)]
        public IdentityType IdentityType { get; set; }        
    }

    public enum IdentityType
    {
        Actor,
        Callback
    }
}