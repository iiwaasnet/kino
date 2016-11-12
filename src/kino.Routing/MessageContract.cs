using kino.Core;

namespace kino.Routing
{
    public class MessageContract
    {
        public Identifier Identifier { get; set; }

        public bool KeepRegistrationLocal { get; set; }
    }
}