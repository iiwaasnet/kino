using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using NetMQ;

namespace Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var messageRouter = new MessageRouter(NetMQContext.Create());
            var actorHost = new ActorHost(NetMQContext.Create());
            var actor = new Actor();
            actorHost.AssignActor(actor);
            actorHost.Stop();

            var requestSink = new ClientRequestSink(NetMQContext.Create());
            //var client = new Client(requestSink);
            //client.

        }
    }
}
