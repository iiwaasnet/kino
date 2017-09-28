#if NET47
using System.Diagnostics;
using System.Management;
using System.Reflection;

namespace kino.Core.Diagnostics.Performance
{

    public class InstanceNameResolver : IInstanceNameResolver
    {
        public string GetInstanceName()
            => GetServiceName()
               ?? $"{Assembly.GetEntryAssembly().GetName().Name}:{Process.GetCurrentProcess().Id}";

        private string GetServiceName()
        {
            var processId = Process.GetCurrentProcess().Id;
            var query = $"SELECT * FROM Win32_Service where ProcessId = {processId}";
            var searcher = new ManagementObjectSearcher(query);

            foreach (var queryObj in searcher.Get())
            {
                return queryObj["Name"].ToString();
            }

            return null;
        }
    }
}
#endif