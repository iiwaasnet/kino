using System;
using System.Collections.Generic;
using kino.Messaging;

namespace kino.Consensus
{
    public interface IIntercomMessageHub
    {
        Listener Subscribe();

        void Send(IMessage message);

        bool Start(TimeSpan startTimeout);

        void Stop();

        IEnumerable<INodeHealthInfo> GetClusterHealthInfo();
    }
}