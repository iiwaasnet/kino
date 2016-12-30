using System;
using Autofac;
using kino;

namespace Client
{
    public class DependencyResolver : IDependencyResolver
    {
        private readonly IComponentContext context;
        private readonly IContainer container;

        public DependencyResolver(IContainer container)
        {
            this.container = container;
            context = null;
        }

        public DependencyResolver(IComponentContext context)
        {
            this.context = context.Resolve<IComponentContext>();
            container = null;
        }

        public T Resolve<T>()
        {
            try
            {
                return container != null
                           ? container.Resolve<T>()
                           : context.Resolve<T>();
            }
            catch (Exception)
            {
                return default(T);
            }
        }
    }
}