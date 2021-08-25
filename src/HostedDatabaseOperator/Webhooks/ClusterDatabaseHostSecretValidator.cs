using System.Threading.Tasks;
using DotnetKubernetesClient;
using HostedDatabaseOperator.Entities;
using k8s.Models;
using KubeOps.Operator.Rbac;
using KubeOps.Operator.Webhooks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Webhooks
{
    [EntityRbac(typeof(V1Secret), Verbs = RbacVerb.Get)]
    public class ClusterDatabaseHostSecretValidator : IValidationWebhook<ClusterDatabaseHost>
    {
        private readonly ILogger<ClusterDatabaseHostSecretValidator> _logger;
        private readonly IKubernetesClient _client;

        public ClusterDatabaseHostSecretValidator(
            ILogger<ClusterDatabaseHostSecretValidator> logger,
            IKubernetesClient client)
        {
            _logger = logger;
            _client = client;
        }

        public AdmissionOperations Operations => AdmissionOperations.Create | AdmissionOperations.Update;

        public Task<ValidationResult> CreateAsync(ClusterDatabaseHost newEntity, bool dryRun) =>
            dryRun ? Task.FromResult(ValidationResult.Success()) : CheckSecretExistence(newEntity);

        public Task<ValidationResult> UpdateAsync(
            ClusterDatabaseHost oldEntity,
            ClusterDatabaseHost newEntity,
            bool dryRun) =>
            dryRun ? Task.FromResult(ValidationResult.Success()) : CheckSecretExistence(newEntity);

        private async Task<ValidationResult> CheckSecretExistence(ClusterDatabaseHost host)
        {
            _logger.LogDebug(@"Check cluster host data of ""{host}"" if the linked secret exists.", host.Name());

            var secret =
                await _client.Get<V1Secret>(
                    host.Spec.CredentialsSecret.Name,
                    host.Spec.CredentialsSecret.NamespaceProperty);

            return secret == null
                ? ValidationResult.Fail(
                    StatusCodes.Status404NotFound,
                    $@"Secret ""{host.Spec.CredentialsSecret.Name}"" in namespace ""{host.Spec.CredentialsSecret.NamespaceProperty}"" not found.")
                : ValidationResult.Success();
        }
    }
}
