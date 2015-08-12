using System;
using System.Threading.Tasks;
using rawf.Messaging;

namespace rawf.Client
{
    public interface IPromise
    {
        Task<IMessage> GetResponse();
        TimeSpan ExpireAfter { get; }
    }
}