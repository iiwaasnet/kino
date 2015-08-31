using kino.Messaging;

namespace kino.Client
{
    internal class CallbackRegistration
    {
        internal IPromise Promise { get; set; }
        internal IMessage Message { get; set; }
        internal ICallbackPoint CallbackPoint { get; set; }
    }
}