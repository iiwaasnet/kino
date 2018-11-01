using kino.Core.Framework;

namespace kino.Consensus
{
    public partial class RoundBasedRegister
    {
        private const string format = "HH:mm:ss fff";

        private void LogNackRead(Ballot ballot)
        {
            if (writeBallot >= ballot)
            {
                logger.Debug($"process {synodConfigProvider.LocalNode.Uri} " +
                             "NACK_READ ==WB== " +
                             $"{writeBallot.Timestamp.ToString(format)}-" +
                             $"{writeBallot.MessageNumber}-" +
                             $"{writeBallot.Identity.GetAnyString()} " +
                             ">= " +
                             $"{ballot.Timestamp.ToString(format)}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetAnyString()}");
            }
            if (readBallot >= ballot)
            {
                logger.Debug($"process {synodConfigProvider.LocalNode.Uri} " +
                             "NACK_READ ==RB== " +
                             $"{readBallot.Timestamp.ToString(format)}-" +
                             $"{readBallot.MessageNumber}-" +
                             $"{readBallot.Identity.GetAnyString()} " +
                             ">= " +
                             $"{ballot.Timestamp.ToString(format)}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetAnyString()}");
            }
        }

        private void LogAckRead(Ballot ballot)
        {
            if (writeBallot < ballot)
            {
                logger.Debug($"process {synodConfigProvider.LocalNode.Uri} " +
                             "ACK_READ ==WB== " +
                             $"{writeBallot.Timestamp.ToString(format)}-" +
                             $"{writeBallot.MessageNumber}-" +
                             $"{writeBallot.Identity.GetAnyString()} " +
                             "< " +
                             $"{ballot.Timestamp.ToString(format)}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetAnyString()}");
            }
            if (readBallot < ballot)
            {
                logger.Debug($"process {synodConfigProvider.LocalNode.Uri} " +
                             "ACK_READ ==RB== " +
                             $"{readBallot.Timestamp.ToString(format)}-" +
                             $"{readBallot.MessageNumber}-" +
                             $"{readBallot.Identity.GetAnyString()} " +
                             "< " +
                             $"{ballot.Timestamp.ToString(format)}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetAnyString()}");
            }
        }

        private void LogNackWrite(Ballot ballot)
        {
            if (writeBallot > ballot)
            {
                logger.Debug($"process {synodConfigProvider.LocalNode.Uri} " +
                             "NACK_WRITE ==WB== " +
                             $"{writeBallot.Timestamp.ToString(format)}-" +
                             $"{writeBallot.MessageNumber}-" +
                             $"{writeBallot.Identity.GetAnyString()} " +
                             "> " +
                             $"{ballot.Timestamp.ToString(format)}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetAnyString()}");
            }
            if (readBallot > ballot)
            {
                logger.Debug($"process {synodConfigProvider.LocalNode.Uri} " +
                             "NACK_WRITE ==RB== " +
                             $"{readBallot.Timestamp.ToString(format)}-" +
                             $"{readBallot.MessageNumber}-" +
                             $"{readBallot.Identity.GetAnyString()} " +
                             "> " +
                             $"{ballot.Timestamp.ToString(format)}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetAnyString()}");
            }
        }

        private void LogAckWrite(Ballot ballot)
        {
            if (writeBallot <= ballot)
            {
                logger.Debug($"process {synodConfigProvider.LocalNode.Uri} " +
                             "ACK_WRITE ==WB== " +
                             $"{writeBallot.Timestamp.ToString(format)}-" +
                             $"{writeBallot.MessageNumber}-" +
                             $"{writeBallot.Identity.GetAnyString()} " +
                             "<= " +
                             $"{ballot.Timestamp.ToString(format)}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetAnyString()}");
            }
            if (readBallot <= ballot)
            {
                logger.Debug($"process {synodConfigProvider.LocalNode.Uri} " +
                             "ACK_WRITE ==RB== " +
                             $"{readBallot.Timestamp.ToString(format)}-" +
                             $"{readBallot.MessageNumber}-" +
                             $"{readBallot.Identity.GetAnyString()} " +
                             "<= " +
                             $"{ballot.Timestamp.ToString(format)}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetAnyString()}");
            }
        }
    }
}