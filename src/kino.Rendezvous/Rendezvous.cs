using System;

namespace kino.Rendezvous
{
    public partial class Rendezvous
    {
        private IDependencyResolver resolver;

        public Rendezvous()
            : this(null)
        {
        }

        public Rendezvous(IDependencyResolver resolver)
        {
            this.resolver = resolver;
        }

        public void SetResolver(IDependencyResolver resolver)
            => this.resolver = resolver;

        private void AssertDependencyResolverSet()
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver), "Dependency resolver is not assigned!");
            }
        }

        public IRendezvousService GetRendezvousService()
        {
            AssertDependencyResolverSet();

            return Build();
        }

    }
}