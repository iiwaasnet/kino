using System;

namespace rawf.Connectivity
{
    public interface IConnectivityProvider : IDisposable
    {
        //TODO: Might extract full NetMQContext interface later
        IDisposable GetConnectivityContext();
    }
}