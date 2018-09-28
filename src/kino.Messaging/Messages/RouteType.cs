using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public enum RouteType
    {
        [ProtoEnum] All,
        [ProtoEnum] Internal,
        [ProtoEnum] External
    }
}