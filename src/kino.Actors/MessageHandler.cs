using System.Threading.Tasks;
using kino.Messaging;

namespace kino.Actors
{
    public delegate ValueTask<IActorResult> MessageHandler(IMessage message);
}