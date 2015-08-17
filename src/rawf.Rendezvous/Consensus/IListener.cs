using System;
using rawf.Messaging;

namespace rawf.Rendezvous.Consensus
{
	public interface IListener : IObservable<IMessage>, IDisposable
	{
		//void Start();

		//void Stop();
	}
}