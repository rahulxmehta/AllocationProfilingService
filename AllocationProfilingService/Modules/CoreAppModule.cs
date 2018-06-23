using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net.Http;

namespace AllocationProfilingService.Modules
{
    public class CoreAppModule : Module
    {
        /// <inheritdoc />
        public override void Load(IServiceCollection services)
        {
            var config = new ConfigurationBuilder()
                             .Build();
            
            services.AddSingleton<HttpClient>();
        }
    }
}
