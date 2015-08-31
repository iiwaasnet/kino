using System.Collections.Generic;
using kino.Messaging;

namespace kino.Connectivity
{
    public interface IActorResult
    {
        IEnumerable<IMessage> Messages { get; }
    }
}