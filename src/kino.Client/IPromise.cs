using System;
using System.Threading.Tasks;
using kino.Messaging;

namespace kino.Client
{
    public interface IPromise
    {
        Task<IMessage> GetResponse();
        TimeSpan ExpireAfter { get; }
    }
}