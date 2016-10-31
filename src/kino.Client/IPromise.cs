using System;
using System.Threading.Tasks;
using kino.Core.Messaging;

namespace kino.Client
{
    public interface IPromise : IDisposable
    {
        Task<IMessage> GetResponse();

        CallbackKey CallbackKey { get; }
    }
}