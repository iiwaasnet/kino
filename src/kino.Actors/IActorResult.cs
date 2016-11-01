using System.Collections.Generic;
using kino.Messaging;

namespace kino.Actors
{
    public interface IActorResult
    {
        IEnumerable<IMessage> Messages { get; }
    }
}