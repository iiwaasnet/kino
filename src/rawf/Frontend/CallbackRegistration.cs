using rawf.Messaging;

namespace rawf.Frontend
{
    internal class CallbackRegistration
    {
        internal IPromise Promise { get; set; }
        internal IMessage Message { get; set; }
        internal ICallbackPoint CallbackPoint { get; set; }
    }
}