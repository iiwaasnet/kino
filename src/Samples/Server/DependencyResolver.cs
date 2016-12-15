using System;
using Autofac;
using kino;

namespace Server
{
    public class DependencyResolver : IDependencyResolver
    {
        private readonly IContainer container;

        public DependencyResolver(IContainer container)
        {
            this.container = container;
        }

        public T Resolve<T>()
        {
            try
            {
                return container.Resolve<T>();
            }
            catch (Exception)
            {
                return default(T);
            }
        }
    }
}