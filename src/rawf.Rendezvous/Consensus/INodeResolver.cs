using System;

namespace rawf.Rendezvous.Consensus
{
    public interface INodeResolver : IDisposable
    {
        IProcess ResolveLocalNode();
        IProcess ResolveRemoteNode(INode node);
        INode ResolveRemoteProcess(IProcess process);
    }
}