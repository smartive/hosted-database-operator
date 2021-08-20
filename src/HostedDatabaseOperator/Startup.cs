using HostedDatabaseOperator.Database;
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
                .AddKubernetesOperator(s =>
                {
                    s.Name = "hosted-database-operator";
#if DEBUG
                    s.EnableLeaderElection = false;
#endif
                })
#if DEBUG
                .AddWebhookLocaltunnel()
#endif
                ;

            services.AddSingleton<DatabaseConnectionsPool>();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseKubernetesOperator();
        }
    }
}
