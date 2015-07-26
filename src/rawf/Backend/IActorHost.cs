namespace rawf.Backend
{
    public interface IActorHost
    {
        void AssignActor(IActor actor);
        void Start();
        void Stop();
    }
}