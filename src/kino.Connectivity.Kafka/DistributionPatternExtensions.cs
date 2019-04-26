using System;
using kino.Messaging;

namespace kino.Connectivity.Kafka
{
    public static class DistributionPatternExtensions
    {
        public static DistributionPattern ToDistribution(this short distribution)
        {
            switch (distribution)
            {
                case 1:
                    return DistributionPattern.Unicast;
                case 2:
                    return DistributionPattern.Broadcast;
            }

            throw new NotSupportedException(distribution.ToString());
        }

        public static short ToDistributionCode(this DistributionPattern distribution)
        {
            switch (distribution)
            {
                case DistributionPattern.Unicast:
                    return 1;
                case DistributionPattern.Broadcast:
                    return 2;
            }

            throw new NotSupportedException(distribution.ToString());
        }
    }
}