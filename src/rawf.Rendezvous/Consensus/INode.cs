using System;

namespace rawf.Rendezvous.Consensus
{
    public interface INode
    {
        byte[] SocketIdentity { get; }
        Uri Uri { get; }
    }
}