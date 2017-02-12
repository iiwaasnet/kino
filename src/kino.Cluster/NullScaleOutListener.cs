using System.Diagnostics.CodeAnalysis;

namespace kino.Cluster
{
    [ExcludeFromCodeCoverage]
    public class NullScaleOutListener : IScaleOutListener
    {
        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}