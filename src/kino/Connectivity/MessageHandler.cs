using System.Threading.Tasks;
using kino.Messaging;

namespace kino.Connectivity
{
    public delegate Task<IActorResult> MessageHandler(IMessage message);
}