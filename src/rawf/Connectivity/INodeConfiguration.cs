using System;

namespace rawf.Connectivity
{
    public interface INodeConfiguration
    {
        ClusterMember RouterAddress { get; }
        ClusterMember ScaleOutAddress { get; }
    }
}