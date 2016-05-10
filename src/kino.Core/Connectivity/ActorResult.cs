using System.Collections.Generic;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class ActorResult : IActorResult
    {
        public static readonly ActorResult Empty;

        static ActorResult()
        {
            Empty = new ActorResult();
        }

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