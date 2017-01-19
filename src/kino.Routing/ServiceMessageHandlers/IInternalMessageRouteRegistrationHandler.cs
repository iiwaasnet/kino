namespace kino.Routing.ServiceMessageHandlers
{
    public interface IInternalMessageRouteRegistrationHandler
    {
        void Handle(InternalRouteRegistration routeRegistration);
    }
}