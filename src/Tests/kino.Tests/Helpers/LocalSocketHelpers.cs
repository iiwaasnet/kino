using System;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Core.Framework;
using kino.Messaging;
using Moq;

namespace kino.Tests.Helpers
{
    public static class LocalSocketHelpers
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);

        internal static void WaitUntilMessageSent(this Mock<ILocalSocket<IMessage>> mock)
            => WaitUntilMessageSent(mock, _ => true);

        internal static void WaitUntilMessageSent(this Mock<ILocalSocket<IMessage>> mock, Func<Message, bool> predicate)
        {
            var retryCount = 40;
            Exception error = null;
            do
            {
                AsyncOp.Sleep();
                try
                {
                    mock.Verify(m => m.Send(It.Is<IMessage>(msg => predicate(msg.As<Message>()))), Times.AtLeastOnce());
                }
                catch (Exception err)
                {
                    error = err;
                }
            } while (--retryCount > 0 && error != null);

            if (error != null)
            {
                throw error;
            }
        }

        internal static void SetupMessageReceived(this Mock<ILocalSocket<IMessage>> mock, IMessage messageIn)
            => mock.SetupMessageReceived(messageIn, TimeSpan.Zero);

        internal static void SetupMessageReceived(this Mock<ILocalSocket<IMessage>> mock, IMessage messageIn, TimeSpan receiveAfter)
        {
            var delaySend = receiveAfter != TimeSpan.Zero;
            var waitHandle = new AutoResetEvent(!delaySend);
            mock.Setup(m => m.CanReceive()).Returns(waitHandle);
            mock.Setup(m => m.TryReceive()).Returns(() =>
                                                    {
                                                        waitHandle.Reset();
                                                        return messageIn;
                                                    });
            if (delaySend)
            {
                Task.Delay(receiveAfter).ContinueWith(_ => waitHandle.Set());
            }
        }
    }
}