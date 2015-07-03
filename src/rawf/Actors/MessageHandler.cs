using System.Threading.Tasks;
using rawf.Messaging;

namespace rawf.Actors
{
    public delegate Task<IMessage> MessageHandler(IMessage inMessage);
}