using System.Collections.Generic;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class ActorResult : IActorResult
    {
        public ActorResult(params IMessage[] messages)
        {
            Messages = messages;
        }

        public ActorResult(IMessage result)
            : this(new[] {result})
        {
        }

        public IEnumerable<IMessage> Messages { get; }
    }
}