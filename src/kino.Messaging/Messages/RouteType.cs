using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public enum RouteType
    {
        [ProtoEnum] Internal,
        [ProtoEnum] External,
        [ProtoEnum] All
    }
}