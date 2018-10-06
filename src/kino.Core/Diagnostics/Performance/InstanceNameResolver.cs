using System.Diagnostics;
using System.Reflection;

namespace kino.Core.Diagnostics.Performance
{
    public class InstanceNameResolver : IInstanceNameResolver
    {
        public string GetInstanceName()
            => $"{Assembly.GetEntryAssembly().GetName().Name}:{Process.GetCurrentProcess().Id}";
    }
}