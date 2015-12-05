namespace kino.Actors
{
    public interface IActorHost
    {
        void AssignActor(IActor actor);
        bool CanAssignActor(IActor actor);
        void Start();
        void Stop();
    }
}