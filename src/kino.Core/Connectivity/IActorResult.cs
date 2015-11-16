using System.Collections.Generic;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public interface IActorResult
    {
        IEnumerable<IMessage> Messages { get; }
    }
}