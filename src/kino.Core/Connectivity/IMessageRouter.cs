using System;

namespace kino.Core.Connectivity
{
    public interface IMessageRouter
    {
        bool Start(TimeSpan startTimeout);

        void Stop();
    }
}