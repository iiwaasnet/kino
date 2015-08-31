namespace kino.Connectivity
{
    public interface IRendezvousConfiguration
    {
        RendezvousEndpoints GetCurrentRendezvousServer();
        void SetCurrentRendezvousServer(RendezvousEndpoints currentRendezvousServer);
        void RotateRendezvousServers();
    }
}