using System;

namespace rawf.Client
{
    public interface IMessagHubConfiguration
    {
        Uri RouterUri { get; set; }
    }
}