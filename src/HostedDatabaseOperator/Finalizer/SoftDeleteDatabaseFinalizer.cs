using System.Threading.Tasks;
using DotnetKubernetesClient;
using HostedDatabaseOperator.Entities;
using k8s.Models;
using KubeOps.Operator.Entities.Extensions;
using KubeOps.Operator.Finalizer;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Finalizer
{
    public class SoftDeleteDatabaseFinalizer : IResourceFinalizer<HostedDatabase>
    {
        private readonly ILogger<SoftDeleteDatabaseFinalizer> _logger;
        private readonly IKubernetesClient _client;
        private readonly IFinalizerManager<DanglingDatabase> _finalizerManager;

        public SoftDeleteDatabaseFinalizer(
            ILogger<SoftDeleteDatabaseFinalizer> logger,
            IKubernetesClient client,
            IFinalizerManager<DanglingDatabase> finalizerManager)
        {
            _logger = logger;
            _client = client;
            _finalizerManager = finalizerManager;
        }

        public async Task FinalizeAsync(HostedDatabase entity)
        {
            /*
             * If this finalizer gets called, create a new dangling database from the hosted database
             * and don't delete the actual database. Only remove the entity.
             * Further, update the credentials, such that the dangling database is also owner of the credentials.
             */

            var danglingDb = await _client.Create(
                new DanglingDatabase
                {
                    Metadata =
                    {
                        Name = entity.Name(),
                        NamespaceProperty = entity.Namespace(),
                    },
                    Spec =
                    {
                        OriginalDatabase = entity,
                    },
                });

            await _finalizerManager.RegisterFinalizerAsync<DeleteDatabaseFinalizer>(danglingDb);

            _logger.LogInformation(
                @"Created dangling db for ""{database}"" on host ""{host}"".",
                entity.Status.DbName,
                entity.Spec.Host);

            if (entity.Status.Credentials != null)
            {
                var secret = await _client.Get<V1Secret>(
                    entity.Status.Credentials.Name,
                    entity.Status.Credentials.NamespaceProperty);

                if (secret != null)
                {
                    secret.RemoveOwnerReference(entity);
                    secret.AddOwnerReference(danglingDb.MakeOwnerReference());
                    await _client.Update(secret);
                }
                else
                {
                    _logger.LogWarning(@"Credentials secret for db ""{database}"" not found.", entity.Status.DbName);
                }
            }
        }
    }
}
