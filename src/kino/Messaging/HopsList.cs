using System.Collections.Generic;
using ProtoBuf;

namespace kino.Messaging
{
    [ProtoContract]
    public class HopsList
    {
        [ProtoMember(1)]
        public IEnumerable<Hop> Hops { get; set; }
    }
}