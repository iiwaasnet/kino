using kino.Core.Connectivity;

namespace kino.Actors
{
    public class ActorMessageHandlerIdentifier
    {
        public MessageIdentifier Identifier { get; set; }

        public bool KeepRegistrationLocal { get; set; }
    }
}