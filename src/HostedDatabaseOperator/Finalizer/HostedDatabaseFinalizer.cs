using System.Threading.Tasks;
using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Entities;
using k8s.Models;
using KubeOps.Operator.Client.LabelSelectors;
using KubeOps.Operator.Finalizer;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Finalizer
{
    public class HostedDatabaseFinalizer : ResourceFinalizerBase<HostedDatabase>
    {
        private readonly ILogger<HostedDatabaseFinalizer> _logger;

        public HostedDatabaseFinalizer(ILogger<HostedDatabaseFinalizer> logger)
        {
            _logger = logger;
        }

        public override async Task Finalize(HostedDatabase resource)
        {
            // TODO create a soft-delete for database
            await using var host = ConnectionsManager.GetHost(resource.Spec.Host);

            _logger.LogDebug(
                @"Delete and cleanup database ""{database}"".",
                resource.Metadata.Name);

            var @namespace = resource.Metadata.NamespaceProperty;
            var configMapName = $"{resource.Metadata.Name}-config";

            var configMap = await Client.Get<V1ConfigMap>(configMapName, @namespace);

            var db = configMap?.Data["database"] ?? host.FormatDatabaseName(resource.Metadata.Name);
            var configs = await Client.List<V1ConfigMap>(
                @namespace,
                new EqualsSelector("managed-by", "hosted-database-operator"),
                new EqualsSelector("database-instance", db));
            var secrets = await Client.List<V1Secret>(
                @namespace,
                new EqualsSelector("managed-by", "hosted-database-operator"),
                new EqualsSelector("database-instance", db));

            await host.ClearDatabaseUsers(db);
            _logger.LogDebug(
                @"Deleted users on database ""{database}"".",
                resource.Metadata.Name);
            await host.DeleteDatabase(db);
            _logger.LogDebug(
                @"Deleted database ""{database}"".",
                resource.Metadata.Name);
            await Client.Delete(configs);
            await Client.Delete(secrets);
            _logger.LogDebug(
                @"Deleted config maps and secrets for database ""{database}"".",
                resource.Metadata.Name);
            _logger.LogInformation(
                @"Finalize for database ""{database}"" executed. Removed database, user, config and secrets.",
                resource.Metadata.Name);
        }
    }
}
