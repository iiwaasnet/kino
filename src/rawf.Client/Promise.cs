using System.Threading.Tasks;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;

namespace rawf.Client
{
    public class Promise : IPromise
    {
        private readonly TaskCompletionSource<IMessage> result;

        public Promise()
        {
            result = new TaskCompletionSource<IMessage>();
        }

        public Task<IMessage> GetResponse()
        {
            return result.Task;
        }

        internal void SetResult(IMessage message)
        {
            if (Unsafe.Equals(message.Identity, ExceptionMessage.MessageIdentity))
            {
                var error = message.GetPayload<ExceptionMessage>().Exception;
                result.SetException(error);
            }
            else
            {
                result.SetResult(message);
            }
        }
    }
}