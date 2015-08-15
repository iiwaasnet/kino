using System;
using rawf.Messaging;

namespace rawf.Consensus
{
	public interface IListener : IObservable<IMessage>, IDisposable
	{
		//void Start();

		//void Stop();
	}
}