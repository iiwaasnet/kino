using System.Threading.Tasks;
using kino.Messaging;

namespace kino.Actors
{
    public delegate Task<IActorResult> MessageHandler(IMessage message);
}