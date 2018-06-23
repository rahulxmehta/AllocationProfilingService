using AllocationProfilingService.ContainerBuilder;
using AllocationProfilingService.Modules;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllocationProfilingService.Containers
{
    public class ContainerBuilder:IContainerBuilder
    {
        private readonly IServiceCollection _services;

        public ContainerBuilder()
        {
            this._services = new ServiceCollection();
        }
        public IServiceProvider Build()
        {
            var provider = this._services.BuildServiceProvider();

            return provider;
        }

        public IContainerBuilder RegisterModule(IModule module = null)
        {
            if (module == null)
            {
                module = new Module();
            }

            module.Load(this._services);

            return this;
        }

        
    }
}
