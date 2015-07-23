using System;

namespace rawf.Connectivity
{
    public interface INodeConfiguration
    {
        Uri RouterAddress { get; }
        Uri ScaleOutAddress { get; }
    }
}