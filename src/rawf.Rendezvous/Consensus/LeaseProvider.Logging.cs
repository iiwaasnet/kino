using System;
using rawf.Framework;

namespace rawf.Rendezvous.Consensus
{
    public partial class LeaseProvider
    {
        private void LogAwake()
        {
            logger.Debug($"SLEEP === process {localNode.SocketIdentity.GetString()} " +
                         $"Waked up at {DateTime.UtcNow.ToString("HH:mm:ss fff")}");
        }

        private void LogStartSleep()
        {
            logger.Debug($"SLEEP === process {localNode.SocketIdentity.GetString()} " +
                         $"Sleep from {DateTime.UtcNow.ToString("HH: mm:ss fff")}");
        }

        private void LogLeaseProlonged(Lease lastReadLease)
        {
            if (lastReadLease != null)
            {
                if (IsLeaseOwner(lastReadLease))
                {
                    logger.Debug($"[{DateTime.UtcNow.ToString("HH:mm:ss fff")}] " +
                                 "PROLONG === process " +
                                 $"{localNode.SocketIdentity.GetString()} " +
                                 "wants to prolong it's lease " +
                                 $"{lastReadLease.ExpiresAt.ToString("HH:mm:ss fff")}");
                }
                else
                {
                    logger.Debug($"[{DateTime.UtcNow.ToString("HH:mm:ss fff")}] " +
                                 "RENEW === process " +
                                 $"{localNode.SocketIdentity.GetString()} " +
                                 "wants to renew lease " +
                                 $"{lastReadLease.ExpiresAt.ToString("HH:mm:ss fff")}");
                }
            }
        }
    }
}