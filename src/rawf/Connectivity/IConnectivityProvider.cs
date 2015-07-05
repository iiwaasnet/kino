using System;
using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IConnectivityProvider : IDisposable
    {
        //TODO: Might extract full NetMQContext interface later
        IDisposable GetConnectivityContext();
        string GetLocalEndpointAddress();
        string GetLocalPeerAddress();
        IEnumerable<string> GetPeerAddresses();
    }
}