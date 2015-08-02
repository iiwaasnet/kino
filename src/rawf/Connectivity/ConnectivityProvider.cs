using System;
using System.Collections.Generic;
using System.Linq;
using rawf.Framework;
using rawf.Sockets;

namespace rawf.Connectivity
{
    //public class ConnectivityProvider : IConnectivityProvider
    //{
    //    private readonly IClusterConfiguration clusterConfiguration;
    //    private readonly IRouterConfiguration routerConfiguration;
    //    private readonly ISocketFactory socketFactory;
    //    private readonly IRendezvousConfiguration rendezvousConfiguration;
    //    private readonly RendezvousServerConfiguration currentRendezvousServer;

    //    public ConnectivityProvider(ISocketFactory socketFactory,
    //                                IRouterConfiguration routerConfiguration,
    //                                IClusterConfiguration clusterConfiguration,
    //                                IRendezvousConfiguration rendezvousConfiguration)
    //    {
    //        this.socketFactory = socketFactory;
    //        this.routerConfiguration = routerConfiguration;
    //        this.clusterConfiguration = clusterConfiguration;
    //        this.rendezvousConfiguration = rendezvousConfiguration;
    //        currentRendezvousServer = rendezvousConfiguration.GetRendezvousServers().First();
    //    }

    //    public ISocket CreateRouterSocket()
    //    {
    //        var socket = socketFactory.CreateRouterSocket();
    //        socket.SetMandatoryRouting();
    //        socket.SetIdentity(routerConfiguration.RouterAddress.Identity);
    //        socket.Bind(routerConfiguration.RouterAddress.Uri);

    //        return socket;
    //    }

    //    public ISocket CreateScaleOutFrontendSocket()
    //    {
    //        var socket = socketFactory.CreateRouterSocket();
    //        socket.SetIdentity(routerConfiguration.ScaleOutAddress.Identity);
    //        socket.SetMandatoryRouting();
    //        socket.Connect(routerConfiguration.RouterAddress.Uri);
    //        socket.Bind(routerConfiguration.ScaleOutAddress.Uri);

    //        return socket;
    //    }

    //    public ISocket CreateScaleOutBackendSocket()
    //    {
    //        var socket = socketFactory.CreateRouterSocket();
    //        socket.SetIdentity(routerConfiguration.ScaleOutAddress.Identity);
    //        socket.SetMandatoryRouting();
    //        foreach (var peer in clusterConfiguration.GetClusterMembers())
    //        {
    //            socket.Connect(peer.Uri);
    //        }

    //        return socket;
    //    }

    //    public ISocket CreateRoutableSocket()
    //    {
    //        var socket = socketFactory.CreateDealerSocket();
    //        socket.SetIdentity(SocketIdentifier.CreateNew());
    //        socket.Connect(routerConfiguration.RouterAddress.Uri);

    //        return socket;
    //    }

    //    public ISocket CreateOneWaySocket()
    //    {
    //        var socket = socketFactory.CreateDealerSocket();
    //        socket.Connect(routerConfiguration.RouterAddress.Uri);

    //        return socket;
    //    }

    //    public ISocket CreateClusterMonitorSendingSocket()
    //    {
    //        var socket = socketFactory.CreateDealerSocket();
    //        socket.SetIdentity(currentRendezvousServer.UnicastUri.Identity);
    //        socket.Connect(currentRendezvousServer.UnicastUri.Uri);

    //        return socket;
    //    }

    //    public ISocket CreateClusterMonitorSubscriptionSocket()
    //    {
    //        var socket = socketFactory.CreateSubscriberSocket();
    //        socket.Connect(currentRendezvousServer.BroadcastUri);

    //        return socket;
    //    }

    //    public IEnumerable<NodeIdentity> GetClusterIdentities()
    //    {
    //        return clusterConfiguration.GetClusterMembers().Select(m => new NodeIdentity {Value = m.Identity});
    //    }

    //    public ISocket CreateRendezvousBroadcastSocket()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public ISocket CreateRendezvousUnicastSocket()
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}