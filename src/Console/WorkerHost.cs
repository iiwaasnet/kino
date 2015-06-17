using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Console.Messages;
using NetMQ;

namespace Console
{
    public class WorkerHost : IWorkerHost
    {
        private IWorker worker;
        private IDictionary<string, MessageHandler> messageHandlers;
        private NetMQSocket requestSocket;
        private NetMQSocket responseSocket;
        private readonly NetMQContext context;
        private const string requestEndpoint = "inproc://backend";
        private const string responseEndpoint = "inproc://frontend";
        private readonly TimeSpan receiveTimeout;
        private Task<Poller> pollerTask;

        public WorkerHost(NetMQContext context)
        {
            this.context = context;
        }

        public void AssignWorker(IWorker worker)
        {
            messageHandlers = BuildMessageHandlersMap(worker);
            this.worker = worker;
        }

        private static IDictionary<string, MessageHandler> BuildMessageHandlersMap(IWorker worker)
        {
            return worker.GetInterfaceDefinition().ToDictionary(d => d.Message.Type, d => d.Handler);
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
            requestSocket = CreateSocket(RequestArrived);
            requestSocket.Connect(requestEndpoint);

            responseSocket = CreateSocket(ResponseConfirmed);
            responseSocket.Connect(responseEndpoint);

            var poller = new Poller(requestSocket, responseSocket);

            SignalWorkerReady(requestSocket);

            poller.Start();

            return poller;
        }

        private NetMQSocket CreateSocket(EventHandler<NetMQSocketEventArgs> inMessageHandler)
        {
            var socket = context.CreateDealerSocket();
            socket.Options.RouterMandatory = true;
            socket.Options.Identity = Guid.NewGuid().ToByteArray();
            responseSocket.ReceiveReady += inMessageHandler;

            return socket;
        }

        private void ResponseConfirmed(object sender, NetMQSocketEventArgs e)
        {
            e.Socket.ReceiveMessage(receiveTimeout);
        }

        private void SignalWorkerReady(NetMQSocket socket)
        {
            var payload = new WorkerReadyMessage.Payload
                          {
                              IncomeMessages = messageHandlers.Keys
                          };
            var multipartMessage = new MultipartMessage(new WorkerReadyMessage(payload));
            socket.SendMessage(new NetMQMessage(multipartMessage.Frames));
        }

        private void RequestArrived(object sender, NetMQSocketEventArgs e)
        {
            var msg = e.Socket.ReceiveMessage(receiveTimeout);
            var multipart = new MultipartMessage(msg);
            var msgType = multipart.GetMessageIdentity();
            var handler = messageHandlers[msgType];

            var messageOut = handler(new Message(multipart.GetMessageTypeBytes(), msgType));

            var response = new MultipartMessage(messageOut);
            responseSocket.SendMessage(new NetMQMessage(response.Frames));

            SignalWorkerReady(e.Socket);
        }
    }
}