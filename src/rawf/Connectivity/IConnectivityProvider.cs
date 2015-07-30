using System.Collections.Generic;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public interface IConnectivityProvider
    {
        ISocket CreateRouterSocket();
        ISocket CreateScaleOutFrontendSocket();
        ISocket CreateScaleOutBackendSocket();
        ISocket CreateRoutableSocket();
        ISocket CreateOneWaySocket();
        ISocket CreateRendezvousSubscriptionSocket();
        ISocket CreateRendezvousSendingSocket();
        IEnumerable<NodeIdentity> GetClusterIdentities();
    }
}