using System.Threading.Tasks;
using DotnetKubernetesClient;
using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Entities;
using k8s.Models;
using KubeOps.Operator.Entities.Extensions;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Finalizer
{
    [EntityRbac(typeof(V1Secret), Verbs = RbacVerb.Get)]
    public class DeleteDatabaseFinalizer : IResourceFinalizer<HostedDatabase>, IResourceFinalizer<DanglingDatabase>
    {
        private readonly ILogger<DeleteDatabaseFinalizer> _logger;
        private readonly DatabaseConnectionPool _pool;
        private readonly IKubernetesClient _client;

        public DeleteDatabaseFinalizer(
            ILogger<DeleteDatabaseFinalizer> logger,
            DatabaseConnectionPool pool,
            IKubernetesClient client)
        {
            _logger = logger;
            _pool = pool;
            _client = client;
        }

        public async Task FinalizeAsync(HostedDatabase entity)
        {
            _logger.LogDebug(
                @"Delete and cleanup database ""{database}"".",
                entity.Name());

            V1Secret? credentials = null;
            if (entity.Status.Credentials != null)
            {
                _logger.LogTrace(
                    @"Delete credentials secret ""{name}"".",
                    entity.Status.Credentials.Name);
                credentials = await _client.Get<V1Secret>(
                    entity.Status.Credentials.Name,
                    entity.Status.Credentials.NamespaceProperty);
                // The credentials are not required to be deleted.
                // There is an owner reference in place which deletes the secret.
            }

            _logger.LogTrace(
                @"Delete database ""{name}"" with user ""{user}"".",
                entity.Status.DbName,
                credentials?.ReadData("username"));
            await using var host = _pool.GetHost(entity.Spec.Host);
            await host.RemoveDatabaseWithUser(entity.Status.DbName, credentials?.ReadData("username"));

            _logger.LogInformation(
                @"Finalize for database ""{database}"" executed. Removed database, user and secrets.",
                entity.Name());
        }

        public Task FinalizeAsync(DanglingDatabase entity)
            => FinalizeAsync(entity.Spec.OriginalDatabase);
    }
}
