using Console.Messages;

namespace Console
{
    internal class Client
    {
        private readonly MessageHub requestSink;

        public Client(MessageHub requestSink)
        {
            this.requestSink = requestSink;
        }

        public IPromise Send(IMessage message, ICallbackPoint callbackPoint)
        {
            return requestSink.EnqueueRequest(message, callbackPoint);
        }
    }
}