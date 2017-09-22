namespace kino.Rendezvous.Service
{
    public class Program
    {
        static void Main(string[] args)
        {
#if NET47
            new ServiceHost().Run();
#endif
        }
    }
}