using rawf.Framework;

namespace rawf.Rendezvous.Consensus
{
    public partial class RoundBasedRegister
    {
        private void LogNackRead(Ballot ballot)
        {
            if (writeBallot >= ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.SocketIdentity.GetString()} "
                             + "NACK_READ ==WB== " +
                             $"{writeBallot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{writeBallot.MessageNumber}-" +
                             $"{writeBallot.Identity.GetString()} "
                             + ">= "
                             + $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetString()}");
            }
            if (readBallot >= ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.SocketIdentity.GetString()} "
                             + "NACK_READ ==RB== "
                             + $"{readBallot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{readBallot.MessageNumber}-" +
                             $"{readBallot.Identity.GetString()} "
                             + ">= "
                             + $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-" +
                             $"{ballot.MessageNumber}-" +
                             $"{ballot.Identity.GetString()}");
            }
        }

        private void LogAckRead(Ballot ballot)
        {
            if (writeBallot < ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.SocketIdentity.GetString()} "
                             + "ACK_READ ==WB== "
                             + $"{writeBallot.Timestamp.ToString("HH: mm:ss fff")}-"
                             + $"{writeBallot.MessageNumber}-"
                             + $"{writeBallot.Identity.GetString()} "
                             + "< "
                             + $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{ballot.MessageNumber}-"
                             + $"{ballot.Identity.GetString()}");
            }
            if (readBallot < ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.SocketIdentity.GetString()} "
                             + "ACK_READ ==RB== "
                             + $"{readBallot.Timestamp.ToString("HH: mm:ss fff")}-"
                             + $"{readBallot.MessageNumber}-"
                             + $"{readBallot.Identity.GetString()} "
                             + "< "
                             + $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{ballot.MessageNumber}-"
                             + $"{ballot.Identity.GetString()}");
            }
        }

        private void LogNackWrite(Ballot ballot)
        {
            if (writeBallot > ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.SocketIdentity.GetString()} "
                             + "NACK_WRITE ==WB== "
                             + $"{writeBallot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{writeBallot.MessageNumber}-"
                             + $"{writeBallot.Identity.GetString()} "
                             + "> "
                             + $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{ballot.MessageNumber}-"
                             + $"{ballot.Identity.GetString()}");
            }
            if (readBallot > ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.SocketIdentity.GetString()} "
                             + "NACK_WRITE ==RB== "
                             + $"{readBallot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{readBallot.MessageNumber}-"
                             + $"{readBallot.Identity.GetString()} "
                             + "> "
                             + $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{ballot.MessageNumber}-"
                             + $"{ballot.Identity.GetString()}");
            }
        }

        private void LogAckWrite(Ballot ballot)
        {
            if (writeBallot <= ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.SocketIdentity.GetString()} "
                             + "ACK_WRITE ==WB== "
                             + $"{writeBallot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{writeBallot.MessageNumber}-"
                             + $"{writeBallot.Identity.GetString()} "
                             + "<= "
                             + $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{ballot.MessageNumber}-"
                             + $"{ballot.Identity.GetString()}");
            }
            if (readBallot <= ballot)
            {
                logger.Debug($"process {synodConfig.LocalNode.SocketIdentity.GetString()} "
                             + "ACK_WRITE ==RB== "
                             + $"{readBallot.Timestamp.ToString("HH: mm:ss fff")}-"
                             + $"{readBallot.MessageNumber}-"
                             + $"{readBallot.Identity.GetString()} "
                             + "<= "
                             + $"{ballot.Timestamp.ToString("HH:mm:ss fff")}-"
                             + $"{ballot.MessageNumber}-"
                             + $"{ballot.Identity.GetString()}");
            }
        }
    }
}