using System;

namespace rawf.Consensus
{
    public interface INode
    {
        byte[] SocketIdentity { get; }
        Uri Uri { get; }
    }
}