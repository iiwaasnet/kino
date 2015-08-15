using System;
using rawf.Framework;

namespace rawf.Consensus
{
    public partial class LeaseProvider
    {
        private void LogAwake()
        {
            logger.DebugFormat("SLEEP === process {0} Waked up at {1}", localNode.SocketIdentity.GetString(), DateTime.UtcNow.ToString("HH:mm:ss fff"));
        }

        private void LogStartSleep()
        {
            logger.DebugFormat("SLEEP === process {0} Sleep from {1}", localNode.SocketIdentity.GetString(), DateTime.UtcNow.ToString("HH:mm:ss fff"));
        }

        private void LogLeaseProlonged(ILease lastReadLease)
        {
            if (lastReadLease != null)
            {
                if (IsLeaseOwner(lastReadLease))
                {
                    logger.DebugFormat("[{0}] PROLONG === process {1} wants to prolong it's lease {2}",
                                       DateTime.UtcNow.ToString("HH:mm:ss fff"),
                                       localNode.SocketIdentity.GetString(),
                                       lastReadLease.ExpiresAt.ToString("HH:mm:ss fff"));
                }
                else
                {
                    logger.DebugFormat("[{0}] RENEW === process {1} wants to renew lease {2}",
                                       DateTime.UtcNow.ToString("HH:mm:ss fff"),
                                       localNode.SocketIdentity.GetString(),
                                       lastReadLease.ExpiresAt.ToString("HH:mm:ss fff"));
                }
            }
        }
    }
}