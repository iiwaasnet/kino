using System.Threading.Tasks;
using rawf.Messaging;

namespace rawf.Frontend
{
    public interface IPromise
    {
        Task<IMessage> GetResponse();
    }
}