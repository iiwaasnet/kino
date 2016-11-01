using System;

namespace kino.Routing
{
    public interface IMessageRouter
    {
        bool Start(TimeSpan startTimeout);

        void Stop();
    }
}