using System;
using System.Threading;
using kino.Connectivity;
using kino.Core.Framework;
using kino.Messaging;
using Moq;

namespace kino.Tests.Helpers
{
    internal static class SocketHelpers
    {
        internal static void SetupMessageReceived(this Mock<ISocket> mock, IMessage message, CancellationToken token)
        {
            var times = 0;
            mock.Setup(m => m.Receive(It.IsAny<CancellationToken>())).Returns(() =>
                                                                                     {
                                                                                         if (times++ == 0)
                                                                                         {
                                                                                             return message;
                                                                                         }
                                                                                         token.WaitHandle.WaitOne();
                                                                                         return null;
                                                                                     });
        }

        internal static void SetupPeriodicMessageReceived(this Mock<ISocket> mock, IMessage message, TimeSpan everyTimeSpan)
            => mock.Setup(m => m.Receive(It.IsAny<CancellationToken>())).Returns(() =>
                                                                                        {
                                                                                            everyTimeSpan.Sleep();
                                                                                            return message;
                                                                                        });
    }
}