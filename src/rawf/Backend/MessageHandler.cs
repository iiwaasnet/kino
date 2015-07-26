using System.Threading.Tasks;
using rawf.Messaging;

namespace rawf.Backend
{
    public delegate Task<IMessage> MessageHandler(IMessage inMessage);
}