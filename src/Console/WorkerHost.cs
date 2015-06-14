using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Console.Messages;
using NetMQ;
using Poller = NetMQ.Poller;

namespace Console
{
    public class WorkerHost : IWorkerHost
    {
        private IWorker worker;
        private IEnumerable<MessageMap> contract;
        private NetMQSocket socket;
        private readonly NetMQContext context;
        private const string localEndpoint = "inproc://backend";
        private readonly TimeSpan pollTimeout;
        private readonly TimeSpan receiveTimeout;
        private Task<Poller> pollerTask;

        public WorkerHost(NetMQContext context)
        {
            this.context = context;
            pollTimeout = TimeSpan.FromSeconds(2);
        }

        public void AssignWorker(IWorker worker)
        {
            contract = worker.GetInterfaceDefinition();
            this.worker = worker;
        }

        public void Start()
        {
            pollerTask = Task<Poller>.Factory.StartNew(StartWorkerHost);
        }

        public void Stop()
        {
            pollerTask.Result.Stop(false);
        }

        private Poller StartWorkerHost()
        {
            socket = context.CreateDealerSocket();
            socket.ReceiveReady += MessageArrived;
            socket.Connect(localEndpoint);
            var poller= new Poller(socket);

            var payload = new WorkerReadyMessage.Payload
                          {
                              IncomeMessages = contract.Select(c => c.Message.Type)
                          };
            var multipartMessage = new MultipartMessage(new WorkerReadyMessage(payload));
            socket.SendMessage(new NetMQMessage(multipartMessage.Frames));

            poller.AddTimer(new NetMQTimer(pollTimeout));
            poller.Start();

            return poller;
        }

        private void MessageArrived(object sender, NetMQSocketEventArgs e)
        {
            var msg = e.Socket.ReceiveMessage(receiveTimeout);
            var multipart = new MultipartMessage(msg);
            var msgType = multipart.GetMessageType();
        }
    }
}