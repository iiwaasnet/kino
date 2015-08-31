using System;
using kino.Messaging;

namespace kino.Rendezvous.Consensus
{
	public class MessageStreamListener : IObserver<IMessage>
	{
		private readonly Action<IMessage> callback;

		public MessageStreamListener(Action<IMessage> callback)
		{
			this.callback = callback;
		}

		public void OnNext(IMessage value)
		{
			callback(value);
		}

		public void OnError(Exception error)
		{
		}

		public void OnCompleted()
		{
		}
	}
}