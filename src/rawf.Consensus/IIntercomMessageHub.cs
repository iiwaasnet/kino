using System;
using rawf.Messaging;

namespace rawf.Consensus
{
	public interface IIntercomMessageHub
	{
	    
		IListener Subscribe();

		void Broadcast(IMessage message);

		void Send(IMessage message, byte[] receiver);
        void Start();
        void Stop();
    }
}