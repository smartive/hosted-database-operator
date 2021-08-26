using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using HostedDatabaseOperator.Entities;
using k8s.Models;
using KubeOps.Operator;
using KubeOps.Operator.Entities.Extensions;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Finalizer
{
    [EntityRbac(typeof(V1Deployment), Verbs = RbacVerb.List)]
    [EntityRbac(typeof(DanglingDatabase), Verbs = RbacVerb.Create)]
    [EntityRbac(typeof(V1Secret), Verbs = RbacVerb.Get | RbacVerb.Update)]
    public class SoftDeleteDatabaseFinalizer : IResourceFinalizer<HostedDatabase>
    {
        private readonly ILogger<SoftDeleteDatabaseFinalizer> _logger;
        private readonly IKubernetesClient _client;
        private readonly IFinalizerManager<DanglingDatabase> _finalizerManager;
        private readonly OperatorSettings _settings;

        public SoftDeleteDatabaseFinalizer(
            ILogger<SoftDeleteDatabaseFinalizer> logger,
            IKubernetesClient client,
            IFinalizerManager<DanglingDatabase> finalizerManager,
            OperatorSettings settings)
        {
            _logger = logger;
            _client = client;
            _finalizerManager = finalizerManager;
            _settings = settings;
        }

        public async Task FinalizeAsync(HostedDatabase entity)
        {
            /*
             * If this finalizer gets called, create a new dangling database from the hosted database
             * and don't delete the actual database. Only remove the entity.
             * Further, update the credentials, such that the dangling database is also owner of the credentials.
             */
            var danglingDb = new DanglingDatabase
            {
                Metadata =
                {
                    Name = entity.Name(),
                    NamespaceProperty = entity.Namespace(),
                    Labels = new Dictionary<string, string>
                    {
                        { "managed-by", _settings.Name },
                        { "hdo.smartive.ch/database-name", entity.Status.DbName ?? throw new("No DB Name set.") },
                    },
                },
                Spec =
                {
                    OriginalDatabase = entity,
                },
            };

            var operatorReference = (await _client.List<V1Deployment>(
                    labelSelectors: new EqualsSelector("operator-deployment", _settings.Name)))
                .SingleOrDefault()
                ?.MakeOwnerReference();

            if (operatorReference != null)
            {
                operatorReference.Controller = true;
                danglingDb.AddOwnerReference(operatorReference);
            }

            danglingDb = await _client.Create(danglingDb);

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
