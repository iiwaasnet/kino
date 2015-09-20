using System;
using kino.Messaging;

namespace kino.Rendezvous.Consensus
{
	public interface IListener : IObservable<IMessage>, IDisposable
	{
	}
}