using System.Collections.Generic;
using kino.Messaging;

namespace kino.Actors
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