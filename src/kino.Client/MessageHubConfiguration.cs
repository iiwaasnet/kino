using System;

namespace kino.Client
{
    public class MessageHubConfiguration : IMessageHubConfiguration
    {
        public Uri RouterUri { get; set; }
    }
}