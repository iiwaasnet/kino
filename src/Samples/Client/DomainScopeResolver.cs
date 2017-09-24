using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Framework;
using kino.Messaging.Messages;
using kino.Security;

namespace Client
{
    public class DomainScopeResolver : IDomainScopeResolver
    {
        public IEnumerable<DomainScope> GetDomainMessages(IEnumerable<string> domains)
        {
            var serverDomain = domains.First();
            var kinoDomain = domains.Second();

            var domainMessageMappings = new List<DomainScope>();

            foreach (var domain in domains)
            {
                var messageIdentities = new List<string>();
                var domainMessages = new DomainScope {Domain = domain, MessageIdentities = messageIdentities};

                for (var i = 0; i < 30; i++)
                {
                    messageIdentities.Add(Guid.NewGuid().ToString());
                }
                if (domain == serverDomain)
                {
                    messageIdentities.Add("EHHLO");
                    messageIdentities.Add("GRPCHARSRESP");
                    messageIdentities.Add("HELLO");
                }
                if (domain == kinoDomain)
                {
                    messageIdentities.Add(KinoMessages.Pong.Identity.GetString());
                    messageIdentities.Add(KinoMessages.Ping.Identity.GetString());
                    messageIdentities.Add(KinoMessages.DiscoverMessageRoute.Identity.GetString());
                    messageIdentities.Add(KinoMessages.Exception.Identity.GetString());
                    messageIdentities.Add(KinoMessages.RegisterExternalMessageRoute.Identity.GetString());
                    messageIdentities.Add(KinoMessages.RequestClusterMessageRoutes.Identity.GetString());
                    messageIdentities.Add(KinoMessages.RequestNodeMessageRoutes.Identity.GetString());
                    messageIdentities.Add(KinoMessages.UnregisterMessageRoute.Identity.GetString());
                    messageIdentities.Add(KinoMessages.UnregisterNode.Identity.GetString());
                    messageIdentities.Add(KinoMessages.RequestExternalRoutes.Identity.GetString());
                    messageIdentities.Add(KinoMessages.RequestMessageExternalRoutes.Identity.GetString());
                    messageIdentities.Add(KinoMessages.MessageExternalRoutes.Identity.GetString());
                }

                domainMessageMappings.Add(domainMessages);
            }

            return domainMessageMappings;
        }
    }
}