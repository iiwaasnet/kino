using System;
using Microsoft.Extensions.Logging;

namespace kino.Consensus
{
    public partial class LeaseProvider
    {
        private const string format = "HH:mm:ss fff";

        private void LogAwake()
        {
            logger.LogDebug($"SLEEP === process {localNode.Uri.AbsoluteUri} " +
                         $"Waked up at {DateTime.UtcNow.ToString(format)}");
        }

        private void LogStartSleep()
        {
            logger.LogDebug($"SLEEP === process {localNode.Uri.AbsoluteUri} " +
                         $"Sleep from {DateTime.UtcNow.ToString(format)}");
        }

        private void LogLeaseProlonged(Lease lastReadLease)
        {
            if (lastReadLease != null)
            {
                if (IsLeaseOwner(lastReadLease))
                {
                    logger.LogDebug($"[{DateTime.UtcNow.ToString(format)}] " +
                                 "PROLONG === process " +
                                 $"{localNode.Uri.AbsoluteUri} " +
                                 "wants to prolong it's lease " +
                                 $"{lastReadLease.ExpiresAt.ToString(format)}");
                }
                else
                {
                    logger.LogDebug($"[{DateTime.UtcNow.ToString(format)}] " +
                                 "RENEW === process " +
                                 $"{localNode.Uri.AbsoluteUri} " +
                                 "wants to renew lease " +
                                 $"{lastReadLease.ExpiresAt.ToString(format)}");
                }
            }
        }
    }
}