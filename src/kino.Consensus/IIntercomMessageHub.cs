using kino.Core.Messaging;

namespace kino.Consensus
{
	public interface IIntercomMessageHub
	{
		Listener Subscribe();
		void Broadcast(IMessage message);
		void Send(IMessage message, byte[] receiver);
        void Start();
        void Stop();
    }
}