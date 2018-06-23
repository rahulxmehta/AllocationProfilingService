using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AllocationProfilingService.Modules;

namespace AllocationProfilingService.ContainerBuilder
{
    /// </summary>
    public interface IContainerBuilder
    {
        /// <summary>
        /// Registers a dependency collection module.
        /// </summary>
        /// <param name="module"><see cref="IModule"/> instance.</param>
        /// <returns>Returns <see cref="IContainerBuilder"/> instance.</returns>
        IContainerBuilder RegisterModule(IModule module = null);

        /// <summary>
        /// Builds the dependency container.
        /// </summary>
        /// <returns>Returns <see cref="IServiceProvider"/> instance.</returns>
        IServiceProvider Build();
    }
}
