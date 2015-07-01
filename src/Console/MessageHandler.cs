using System.Threading.Tasks;
using Console.Messages;

namespace Console
{
    public delegate Task<IMessage> MessageHandler(IMessage inMessage);
}