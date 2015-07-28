namespace rawf.Backend
{
    public interface IRoutingTable
    {
        void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier);
        SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier);
    }
}