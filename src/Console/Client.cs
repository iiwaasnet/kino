using Console.Messages;

namespace Console
{
    internal class Client
    {
        private readonly ClientRequestSink requestSink;

        public Client(ClientRequestSink requestSink)
        {
            this.requestSink = requestSink;
        }

        public ICallbackPoint CreateCallbackPoint(string messageIdentity)
        {
            return new CallbackPoint
                   {
                       MessageIdentity = messageIdentity.GetBytes(),
                       ReceiverIdentity = requestSink.GetReceiverIdentity()
                   };
        }

        public IPromise Send(IMessage message)
        {
            return requestSink.EnqueueRequest(message);
        }
    }
}