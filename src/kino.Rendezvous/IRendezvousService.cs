using System;

namespace kino.Rendezvous
{
    public interface IRendezvousService
    {
        bool Start(TimeSpan startTimeout);

        void Stop();
    }
}