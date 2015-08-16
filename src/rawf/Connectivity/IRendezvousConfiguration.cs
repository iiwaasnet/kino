namespace rawf.Connectivity
{
    public interface IRendezvousConfiguration
    {
        RendezvousServerConfiguration GetCurrentRendezvousServer();
        RendezvousServerConfiguration RotateRendezvousServers();
    }
}