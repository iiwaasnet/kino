using kino.Framework;

namespace kino.Rendezvous.Consensus
{
    public partial class RoundBasedRegister
    {
        private void LogNackRead(Ballot ballot)
        {
            if (writeBallot >= ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.Uri.AbsoluteUri} " +
                             "NACK_READ ==WB== " +
                             $"{writeBallot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{writeBallot.MessageNumber}-" +
                             $"{writeBallot.Identity.GetString()} " +
                             ">= " +
                             $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetString()}");
            }
            if (readBallot >= ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.Uri.AbsoluteUri} " +
                             "NACK_READ ==RB== " +
                             $"{readBallot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{readBallot.MessageNumber}-" +
                             $"{readBallot.Identity.GetString()} " +
                             ">= " +
                             $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetString()}");
            }
        }

        private void LogAckRead(Ballot ballot)
        {
            if (writeBallot < ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.Uri.AbsoluteUri} " +
                             "ACK_READ ==WB== " +
                             $"{writeBallot.Timestamp.ToString("HH: mm:ss fff")}-" +
                             $"{writeBallot.MessageNumber}-" +
                             $"{writeBallot.Identity.GetString()} " +
                             "< " +
                             $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetString()}");
            }
            if (readBallot < ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.Uri.AbsoluteUri} " +
                             "ACK_READ ==RB== " +
                             $"{readBallot.Timestamp.ToString("HH: mm:ss fff")}-" +
                             $"{readBallot.MessageNumber}-" +
                             $"{readBallot.Identity.GetString()} " +
                             "< " +
                             $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetString()}");
            }
        }

        private void LogNackWrite(Ballot ballot)
        {
            if (writeBallot > ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.Uri.AbsoluteUri} " +
                             "NACK_WRITE ==WB== " +
                             $"{writeBallot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{writeBallot.MessageNumber}-" +
                             $"{writeBallot.Identity.GetString()} " +
                             "> " +
                             $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetString()}");
            }
            if (readBallot > ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.Uri.AbsoluteUri} " +
                             "NACK_WRITE ==RB== " +
                             $"{readBallot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{readBallot.MessageNumber}-" +
                             $"{readBallot.Identity.GetString()} " +
                             "> " +
                             $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetString()}");
            }
        }

        private void LogAckWrite(Ballot ballot)
        {
            if (writeBallot <= ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.Uri.AbsoluteUri} " +
                             "ACK_WRITE ==WB== " +
                             $"{writeBallot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{writeBallot.MessageNumber}-" +
                             $"{writeBallot.Identity.GetString()} " +
                             "<= " +
                             $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetString()}");
            }
            if (readBallot <= ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.Uri.AbsoluteUri} " +
                             "ACK_WRITE ==RB== " +
                             $"{readBallot.Timestamp.ToString("HH: mm:ss fff")}-" +
                             $"{readBallot.MessageNumber}-" +
                             $"{readBallot.Identity.GetString()} " +
                             "<= " +
                             $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetString()}");
            }
        }
    }
}