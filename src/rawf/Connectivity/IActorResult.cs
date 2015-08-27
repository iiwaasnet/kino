using System.Collections.Generic;
using rawf.Messaging;

namespace rawf.Connectivity
{
    public interface IActorResult
    {
        IEnumerable<IMessage> Messages { get; }
    }
}