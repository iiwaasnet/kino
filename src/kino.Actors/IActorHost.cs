namespace kino.Actors
{
    public interface IActorHost
    {
        void AssignActor(IActor actor);
        void Start();
        void Stop();
    }
}