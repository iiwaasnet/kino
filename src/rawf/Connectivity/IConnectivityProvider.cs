using System;
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
        ISocket CreateClusterMonitorSubscriptionSocket();
        ISocket CreateClusterMonitorSendingSocket();
        IEnumerable<NodeIdentity> GetClusterIdentities();
        ISocket CreateRendezvousBroadcastSocket();
        ISocket CreateRendezvousUnicastSocket();
    }
}