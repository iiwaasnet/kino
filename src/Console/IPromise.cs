using System.Threading.Tasks;
using Console.Messages;

namespace Console
{
    public interface IPromise<T>
        where T : IMessage
    {
        Task<T> GetResult();
    }
}