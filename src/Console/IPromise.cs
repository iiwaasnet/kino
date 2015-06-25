using System.Threading.Tasks;
using Console.Messages;

namespace Console
{
    public interface IPromise
    {
        Task<IMessage> GetResponse();
    }
}