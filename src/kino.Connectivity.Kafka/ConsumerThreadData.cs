using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace kino.Connectivity.Kafka
{
    internal class ConsumerThreadData
    {
        public IConsumer<Null, byte[]> Consumer { get; set; }

        public CancellationTokenSource TokenSource { get; set; }

        public Task ConsumingTask { get; set; }
    }
}