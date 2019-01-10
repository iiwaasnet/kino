using System.Collections.Concurrent;

namespace kino.Connectivity
{
    public class LocalSocketFactory : ILocalSocketFactory
    {
        private readonly ConcurrentDictionary<string, object> namedSockets = new ConcurrentDictionary<string, object>();

        public ILocalSocket<T> Create<T>()
            => new LocalSocket<T>();

        public ILocalSocket<T> CreateNamed<T>(string name)
            => (ILocalSocket<T>) namedSockets.GetOrAdd(name, n => new LocalSocket<T>());
    }
}