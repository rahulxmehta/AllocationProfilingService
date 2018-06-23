using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace AllocationProfilingService.Modules
{
    public class Module : IModule
    {
        /// <inheritdoc />
        public virtual void Load(IServiceCollection services)
        {
            return;
        }
    }
}
