using System.Collections.Generic;
using kino.Actors;

namespace kino.Tests.Actors.Setup
{
    public class ConfigurableActor : Actor
    {
        private readonly IEnumerable<MessageHandlerDefinition> interfaceDefinition;

        public ConfigurableActor(IEnumerable<MessageHandlerDefinition> interfaceDefinition)
        {
            this.interfaceDefinition = interfaceDefinition;
        }

        public override IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition()
            => interfaceDefinition;
    }
}