using System.Threading.Tasks;
using rawf.Messaging;

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
            result.SetResult(message);
        }
    }
}