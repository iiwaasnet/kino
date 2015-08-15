using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using rawf.Connectivity;
using rawf.Consensus.Messages;
using rawf.Diagnostics;
using rawf.Framework;
using rawf.Messaging;
using Node = rawf.Consensus.Messages.Node;
using Process = wacs.Messaging.Messages.Process;

namespace rawf.Consensus
{
    public class NodeResolver : INodeResolver
    {
        private readonly CancellationTokenSource cancellation;
        private readonly IListener listener;
        private readonly INode localNode;
        private readonly ILogger logger;
        private readonly IIntercomMessageHub intercomMessageHub;
        private readonly ISynodConfiguration synodConfig;
        private readonly ConcurrentDictionary<INode, IProcess> nodeToProcessMap;
        private readonly ConcurrentDictionary<IProcess, INode> processToNodeMap;
        private readonly ISynodConfigurationProvider synodConfigProvider;
        private readonly Thread worldLearningThread;
        private volatile IProcess localProcess;
        private readonly IDisposable subscription;
        private bool disposed;

        public NodeResolver(IIntercomMessageHub intercomMessageHub,
                            ISynodConfiguration synodConfig,
                            ILogger logger)
        {
            this.intercomMessageHub = intercomMessageHub;
            this.logger = logger;
            this.synodConfig = synodConfig;

            localNode = synodConfigProvider.LocalNode;
            localProcess = synodConfigProvider.LocalProcess;
            nodeToProcessMap = new ConcurrentDictionary<INode, IProcess>(new[] {new KeyValuePair<INode, IProcess>(localNode, localProcess)});
            processToNodeMap = new ConcurrentDictionary<IProcess, INode>(new[] {new KeyValuePair<IProcess, INode>(localProcess, localNode)});
            synodConfigProvider.WorldChanged += RemoveDeadNodes;
            this.synodConfigProvider = synodConfigProvider;

            cancellation = new CancellationTokenSource();

            listener = intercomMessageHub.Subscribe();
            subscription = listener.Subscribe(new MessageStreamListener(OnMessage));
            listener.Start();

            worldLearningThread = StartLearningWorld(config);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                try
                {
                    listener.Stop();
                    subscription.Dispose();
                    cancellation.Cancel(false);
                    cancellation.Dispose();
                    worldLearningThread.Join();

                    disposed = true;
                }
                catch
                {
                }
            }
        }

        public IProcess ResolveRemoteNode(INode node)
        {
            IProcess resolved;
            nodeToProcessMap.TryGetValue(node, out resolved);

            return resolved;
        }

        public INode ResolveRemoteProcess(IProcess process)
        {
            INode node;
            processToNodeMap.TryGetValue(process, out node);

            return node;
        }

        public IProcess ResolveLocalNode()
        {
            return localProcess;
        }

        private void RemoveDeadNodes()
        {
            var deadNodes = nodeToProcessMap
                .Where(node => !synodConfigProvider.World.Contains(node.Key))
                .Aggregate(Enumerable.Empty<INode>(), (current, node) => current.Concat(new[] {node.Key}));

            deadNodes.ForEach(RemoveProcessMapping);
        }

        private void RemoveProcessMapping(INode deadNode)
        {
            IProcess val;
            if (nodeToProcessMap.TryRemove(deadNode, out val))
            {
                INode node;
                processToNodeMap.TryRemove(val, out node);
            }
        }

        private Thread StartLearningWorld(INodeResolverConfiguration config)
        {
            var thread = new Thread(() => ResolveWorld(cancellation.Token, config.ProcessIdBroadcastPeriod));
            thread.Start();

            return thread;
        }

        private void ResolveWorld(CancellationToken token, TimeSpan processIdBroadcastPeriod)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    intercomMessageHub
                        .Broadcast(new ProcessAnnouncementMessage(new Process {Id = localProcess.Id},
                                                                  new ProcessAnnouncementMessage.Payload
                                                                  {
                                                                      Node = new Node
                                                                             {
                                                                                 BaseAddress = localNode.BaseAddress,
                                                                                 IntercomPort = localNode.IntercomPort,
                                                                                 ServicePort = localNode.ServicePort
                                                                             },
                                                                      Process = new Process
                                                                                {
                                                                                    Id = localProcess.Id
                                                                                }
                                                                  }));
                    Thread.Sleep(processIdBroadcastPeriod);
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private void OnMessage(IMessage message)
        {
            if (message.Body.MessageType == ProcessAnnouncementMessage.MessageType)
            {
                var ann = new ProcessAnnouncementMessage(message).GetPayload();
                var announcedNode = new Configuration.Node(ann.Node.BaseAddress, ann.Node.IntercomPort, ann.Node.ServicePort);
                var joiningProcess = new Configuration.Process(ann.Process.Id);

                INode registeredNode;
                if (processToNodeMap.TryGetValue(joiningProcess, out registeredNode) && !announcedNode.Equals(registeredNode))
                {
                    //TODO: Add to conflict list to be displayed on management console
                    logger.WarnFormat("Conflicting processes! Existing {0}@{1}, joining {2}@{3}",
                                      ann.Process.Id,
                                      registeredNode.BaseAddress,
                                      ann.Process.Id,
                                      ann.Node.BaseAddress);
                }
                else
                {
                    if (processToNodeMap.TryAdd(joiningProcess, announcedNode))
                    {
                        nodeToProcessMap[announcedNode] = joiningProcess;
                    }
                }
            }
        }
    }
}