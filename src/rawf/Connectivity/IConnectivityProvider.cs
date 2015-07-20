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
        ISocket CreateClientSendingSocket();
        ISocket CreateClientReceivingSocket();
    }
}