using System;

namespace kino.Client
{
    public interface IMessageHubConfiguration
    {
        Uri RouterUri { get; set; }
    }
}