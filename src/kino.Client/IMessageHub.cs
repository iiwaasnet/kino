using kino.Core;
using kino.Messaging;

namespace kino.Client
{
    public interface IMessageHub
    {
        void SendOneWay(IMessage message);

        IPromise Send(IMessage message);

        IPromise Send(IMessage message, CallbackPoint callbackPoint);

        void Start();

        void Stop();

        ReceiverIdentifier ReceiverIdentifier { get; }
    }
}