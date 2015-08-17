namespace rawf.Connectivity
{
    public interface IRendezvousConfiguration
    {
        RendezvousServerConfiguration GetCurrentRendezvousServer();
        void SetCurrentRendezvousServer(RendezvousServerConfiguration currentRendezvousServer);
        void RotateRendezvousServers();
    }
}