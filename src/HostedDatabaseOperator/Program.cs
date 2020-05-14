using System.Threading.Tasks;
using HostedDatabaseOperator.Controller;
using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Entities;
using HostedDatabaseOperator.Finalizer;
using KubeOps.Operator;
using Microsoft.Extensions.DependencyInjection;

namespace HostedDatabaseOperator
{
    public static class Program
    {
        public static Task<int> Main(string[] args) => new KubernetesOperator("hosted-database-operator")
            .ConfigureServices(
                services =>
                {
                    services
                        .AddSingleton<ConnectionsManager>()
                        .AddResourceController<ClusterDatabaseHostController, ClusterDatabaseHost>()
                        .AddResourceController<HostedDatabaseController, HostedDatabase>()
                        .AddResourceFinalizer<HostedDatabaseFinalizer, HostedDatabase>();
                })
            .Run(args);
    }
}
