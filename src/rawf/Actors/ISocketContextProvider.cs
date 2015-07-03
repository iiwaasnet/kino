using System;

namespace rawf.Actors
{
    public interface ISocketContextProvider
    {
        //TODO: Might extract full NetMQContext interface later
        IDisposable GetContext();
    }
}