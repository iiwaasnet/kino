using System.Threading.Tasks;
using rawf.Messaging;

namespace rawf.Connectivity
{
    public delegate Task<IMessage> MessageHandler(IMessage inMessage);
}