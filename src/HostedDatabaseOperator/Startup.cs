using HostedDatabaseOperator.Controller;
using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Finalizer;
using KubeOps.Operator;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace HostedDatabaseOperator
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddKubernetesOperator(s => s.Name = "hosted-database-operator")
                .AddController<ClusterDatabaseHostController>()
                .AddController<HostedDatabaseController>()
                .AddFinalizer<HostedDatabaseFinalizer>();
            services.AddSingleton<ConnectionsManager>();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseKubernetesOperator();
        }
    }
}
