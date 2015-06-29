using System;

namespace Console
{
    public interface ISocketContextProvider
    {
        //TODO: Might extract full NetMQContext interface later
        IDisposable GetContext();
    }
}