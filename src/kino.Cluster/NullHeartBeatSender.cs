using System.Diagnostics.CodeAnalysis;

namespace kino.Cluster
{
    [ExcludeFromCodeCoverage]
    public class NullHeartBeatSender : IHeartBeatSender
    {
        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}