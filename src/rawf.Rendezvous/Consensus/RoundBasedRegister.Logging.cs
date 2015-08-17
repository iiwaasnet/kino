using rawf.Framework;

namespace rawf.Rendezvous.Consensus
{
	public partial class RoundBasedRegister
	{
		private void LogNackRead(Ballot ballot)
		{
			if (writeBallot >= ballot)
			{
				logger.DebugFormat("process {6} NACK_READ ==WB== {0}-{1}-{2} >= {3}-{4}-{5}",
				                   writeBallot.Timestamp.ToString("HH:mm:ss fff"),
				                   writeBallot.MessageNumber,
				                   writeBallot.Identity.GetString(),
				                   ballot.Timestamp.ToString("HH:mm:ss fff"),
				                   ballot.MessageNumber,
				                   ballot.Identity.GetString(),
				                   synodConfig.LocalNode.SocketIdentity.GetString());
			}
			if (readBallot >= ballot)
			{
				logger.DebugFormat("process {6} NACK_READ ==RB== {0}-{1}-{2} >= {3}-{4}-{5}",

				                   readBallot.Timestamp.ToString("HH:mm:ss fff"),
				                   readBallot.MessageNumber,
				                   readBallot.Identity.GetString(),

				                   ballot.Timestamp.ToString("HH:mm:ss fff"),
				                   ballot.MessageNumber,
				                   ballot.Identity.GetString(),

                                   synodConfig.LocalNode.SocketIdentity.GetString());
			}
		}
        
        private void LogAckRead(Ballot ballot)
		{
			if (writeBallot < ballot)
			{
				logger.DebugFormat("process {6} ACK_READ ==WB== {0}-{1}-{2} < {3}-{4}-{5}",

				                   writeBallot.Timestamp.ToString("HH:mm:ss fff"),
				                   writeBallot.MessageNumber,
				                   writeBallot.Identity.GetString(),

				                   ballot.Timestamp.ToString("HH:mm:ss fff"),
				                   ballot.MessageNumber,
				                   ballot.Identity.GetString(),

                                   synodConfig.LocalNode.SocketIdentity.GetString());
			}
			if (readBallot < ballot)
			{
				logger.DebugFormat("process {6} ACK_READ ==RB== {0}-{1}-{2} < {3}-{4}-{5}",

				                   readBallot.Timestamp.ToString("HH:mm:ss fff"),
				                   readBallot.MessageNumber,
				                   readBallot.Identity.GetString(),

				                   ballot.Timestamp.ToString("HH:mm:ss fff"),
				                   ballot.MessageNumber,
				                   ballot.Identity.GetString(),

                                   synodConfig.LocalNode.SocketIdentity.GetString());
			}
		}

        private void LogNackWrite(Ballot ballot)
        {
            if (writeBallot > ballot)
            {
                logger.DebugFormat("process {6} NACK_WRITE ==WB== {0}-{1}-{2} > {3}-{4}-{5}",

                                   writeBallot.Timestamp.ToString("HH:mm:ss fff"),
                                   writeBallot.MessageNumber,
                                   writeBallot.Identity.GetString(),

                                   ballot.Timestamp.ToString("HH:mm:ss fff"),
                                   ballot.MessageNumber,
                                   ballot.Identity.GetString(),

                                   synodConfig.LocalNode.SocketIdentity.GetString());
            }
            if (readBallot > ballot)
            {
                logger.DebugFormat("process {6} NACK_WRITE ==RB== {0}-{1}-{2} > {3}-{4}-{5}",

                                   readBallot.Timestamp.ToString("HH:mm:ss fff"),
                                   readBallot.MessageNumber,
                                   readBallot.Identity.GetString(),

                                   ballot.Timestamp.ToString("HH:mm:ss fff"),
                                   ballot.MessageNumber,
                                   ballot.Identity.GetString(),

                                   synodConfig.LocalNode.SocketIdentity.GetString());
            }
        }

        private void LogAckWrite(Ballot ballot)
        {
            if (writeBallot <= ballot)
            {
                logger.DebugFormat("process {6} ACK_WRITE ==WB== {0}-{1}-{2} <= {3}-{4}-{5}",

                                   writeBallot.Timestamp.ToString("HH:mm:ss fff"),
                                   writeBallot.MessageNumber,
                                   writeBallot.Identity.GetString(),

                                   ballot.Timestamp.ToString("HH:mm:ss fff"),
                                   ballot.MessageNumber,
                                   ballot.Identity.GetString(),

                                   synodConfig.LocalNode.SocketIdentity.GetString());
            }
            if (readBallot <= ballot)
            {
                logger.DebugFormat("process {6} ACK_WRITE ==RB== {0}-{1}-{2} <= {3}-{4}-{5}",

                                   readBallot.Timestamp.ToString("HH:mm:ss fff"),
                                   readBallot.MessageNumber,
                                   readBallot.Identity.GetString(),

                                   ballot.Timestamp.ToString("HH:mm:ss fff"),
                                   ballot.MessageNumber,
                                   ballot.Identity.GetString(),

                                   synodConfig.LocalNode.SocketIdentity.GetString());
            }
        }
	}
}