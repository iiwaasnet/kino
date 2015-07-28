using System.Collections.Generic;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public interface IConnectivityProvider
    {
        ISocket CreateRouterSocket();
        ISocket CreateScaleOutFrontendSocket();
        ISocket CreateScaleOutBackendSocket();
        ISocket CreateActorSyncSocket();
        ISocket CreateActorAsyncSocket();
        ISocket CreateMessageHubSendingSocket();
        ISocket CreateMessageHubReceivingSocket();
        ISocket CreateRendezvousSubscriptionSocket();
        ISocket CreateRendezvousSendingSocket();
        IEnumerable<NodeIdentity> GetClusterIdentities();
    }
}