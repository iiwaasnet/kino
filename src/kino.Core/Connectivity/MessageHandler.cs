using System.Threading.Tasks;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public delegate Task<IActorResult> MessageHandler(IMessage message);
}