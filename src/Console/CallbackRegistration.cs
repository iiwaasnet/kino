using Console.Messages;

namespace Console
{
    internal class CallbackRegistration
    {
        internal IPromise Promise { get; set; }
        internal IMessage Message { get; set; }
    }
}