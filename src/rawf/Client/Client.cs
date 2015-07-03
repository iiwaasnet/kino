using rawf.Messaging;

namespace rawf.Client
{
    public class Client
    {
        private readonly MessageHub messageHub;

        public Client(MessageHub messageHub)
        {
            this.messageHub = messageHub;
        }

        public IPromise Send(IMessage message, ICallbackPoint callbackPoint)
        {
            return messageHub.EnqueueRequest(message, callbackPoint);
        }
    }
}