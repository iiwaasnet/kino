using System;

namespace rawf.Client
{
    public interface IMessageHubConfiguration
    {
        Uri RouterUri { get; set; }
    }
}