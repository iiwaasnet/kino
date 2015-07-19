using System;
using NetMQ;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public interface IConnectivityProvider
    {
        ISocket CreateRouterSocket();
        ISocket CreateFrontendScaleOutSocket();
        ISocket CreateBackendScaleOutSocket();
        ISocket CreateActorSyncSocket();
        ISocket CreateActorAsyncSocket();
        ISocket CreateClientSendingSocket();
        ISocket CreateClientReceivingSocket();

    }
}