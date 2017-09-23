using kino.Connectivity;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    
    public class LocalSocketTests
    {
        private LocalSocket<IMessage> socket;
        private Mock<IPerformanceCounter> receivingRate;
        private Mock<IPerformanceCounter> sendingRate;

        
        public void Setup()
        {
            sendingRate = new Mock<IPerformanceCounter>();
            receivingRate = new Mock<IPerformanceCounter>();
            socket = new LocalSocket<IMessage>
                     {
                         SendRate = sendingRate.Object,
                         ReceiveRate = receivingRate.Object
                     };
        }

        [Fact]
        public void WhenSendIsCalled_SendingRatePerformanceCounterIsIncremented()
        {
            var times = Randomizer.Int32(3, 5);
            for (var i = 0; i < times; i++)
            {
                socket.Send(Message.Create(new AddPeerMessage()));
            }
            //
            sendingRate.Verify(m => m.Increment(1), Times.Exactly(times));
            receivingRate.Verify(m => m.Increment(1), Times.Never);
        }

        [Fact]
        public void WhenTryReceiveIsCalled_ReceivingRatePerformanceCounterIsIncremented()
        {
            var times = Randomizer.Int32(3, 5);
            for (var i = 0; i < times; i++)
            {
                socket.TryReceive();
            }
            //
            receivingRate.Verify(m => m.Increment(1), Times.Exactly(times));
        }
    }
}