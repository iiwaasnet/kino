using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace rawf.Consensus
{
    public class SynodConfigurationProvider : ISynodConfigurationProvider
    {
        private readonly EventHandlerList eventHandlers;
        private static readonly object WorldChangedEvent = new object();
        private static readonly object SynodChangedEvent = new object();
        private ConcurrentDictionary<INode, object> world;
        private ConcurrentDictionary<INode, object> synod;
        private readonly IProcess localProcess;
        private readonly INode localNode;
        private readonly object locker = new object();

        public SynodConfigurationProvider(ISynodConfiguration config)
        {
            eventHandlers = new EventHandlerList();

            localProcess = new Process();
            localNode = new Node(config.LocalNode);
            var dictionary = config.Synod.Members.ToDictionary(n => (INode) new Node(n), n => (object) null);

            AssertNotEmptySynodIncludesLocalNode(dictionary.Keys);

            synod = new ConcurrentDictionary<INode, object>(dictionary);
            world = new ConcurrentDictionary<INode, object>(dictionary);
            EnsureInitialWorldIncludesLocalNode(world, localNode);
        }

        private void EnsureInitialWorldIncludesLocalNode(ConcurrentDictionary<INode, object> world, INode localNode)
        {
            world.TryAdd(localNode, null);
        }

        private void AssertNotEmptySynodIncludesLocalNode(IEnumerable<INode> synod)
        {
            if (synod != null && synod.Any() && !synod.Any(ep => ep.GetServiceAddress() == localNode.GetServiceAddress()))
            {
                throw new Exception(string.Format("Synod should be empty or include local process {0}!", localNode.GetServiceAddress()));
            }
        }

        public void ActivateNewSynod(IEnumerable<INode> newSynod)
        {
            lock (locker)
            {
                var tmpSynod = newSynod.ToDictionary(n => (INode) new Node(n), n => (object) null);

                var tmpWorld = MergeNewSynodAndRemainedWorld(tmpSynod, RemoveOldSynodFromWorld());

                synod = new ConcurrentDictionary<INode, object>(tmpSynod);
                world = new ConcurrentDictionary<INode, object>(tmpWorld);

                OnSynodChanged();
                OnWorldChanged();
            }
        }

        private IEnumerable<KeyValuePair<INode, object>> MergeNewSynodAndRemainedWorld(Dictionary<INode, object> tmpSynod,
                                                                                           IDictionary<INode, object> tmpWorld)
        {
            foreach (var node in tmpSynod)
            {
                tmpWorld[node.Key] = null;
            }

            return tmpWorld;
        }

        private IDictionary<INode, object> RemoveOldSynodFromWorld()
        {
            return world
                .Where(pair => !synod.ContainsKey(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public void AddNodeToWorld(INode newNode)
        {
            lock (locker)
            {
                if (world.TryAdd(newNode, null))
                {
                    OnWorldChanged();
                }
            }
        }

        public void DetachNodeFromWorld(INode detachedNode)
        {
            lock (locker)
            {
                AssertNodeIsNotInSynode(detachedNode);

                object value;
                if (world.TryRemove(detachedNode, out value))
                {
                    OnWorldChanged();
                }
            }
        }

        public bool IsMemberOfSynod(INode node)
        {
            return synod.ContainsKey(node);
        }


        private void AssertNodeIsNotInSynode(INode detachedNode)
        {
            if (synod.ContainsKey(detachedNode))
            {
                throw new Exception(string.Format("Unable to detach process from world! process {0} is part of the synod.", detachedNode.GetServiceAddress()));
            }
        }

        private void OnWorldChanged()
        {
            var handler = eventHandlers[WorldChangedEvent] as WorldChangedHandler;
            if (handler != null)
            {
                handler();
            }
        }

        private void OnSynodChanged()
        {
            var handler = eventHandlers[SynodChangedEvent] as WorldChangedHandler;
            if (handler != null)
            {
                handler();
            }
        }

        public event WorldChangedHandler WorldChanged
        {
            add { eventHandlers.AddHandler(WorldChangedEvent, value); }
            remove { eventHandlers.RemoveHandler(WorldChangedEvent, value); }
        }

        public event WorldChangedHandler SynodChanged
        {
            add { eventHandlers.AddHandler(SynodChangedEvent, value); }
            remove { eventHandlers.RemoveHandler(SynodChangedEvent, value); }
        }

        public IEnumerable<INode> World
        {
            get { return world.Keys; }
        }

        public IEnumerable<INode> Synod
        {
            get { return synod.Keys; }
        }

        public IProcess LocalProcess
        {
            get { return localProcess; }
        }

        public INode LocalNode
        {
            get { return localNode; }
        }
    }
}