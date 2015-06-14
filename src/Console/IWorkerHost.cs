namespace Console
{
    public interface IWorkerHost
    {
        void AssignWorker(IWorker worker);
        void Start();
    }
}