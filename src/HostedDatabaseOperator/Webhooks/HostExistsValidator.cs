using System.Threading.Tasks;
using DotnetKubernetesClient;
using HostedDatabaseOperator.Entities;
using KubeOps.Operator.Rbac;
using KubeOps.Operator.Webhooks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Webhooks
{
    [EntityRbac(typeof(ClusterDatabaseHost), Verbs = RbacVerb.Get)]
    public class HostExistsValidator : IValidationWebhook<HostedDatabase>, IValidationWebhook<DanglingDatabase>
    {
        private readonly ILogger<HostExistsValidator> _logger;
        private readonly IKubernetesClient _client;

        public HostExistsValidator(ILogger<HostExistsValidator> logger, IKubernetesClient client)
        {
            _logger = logger;
            _client = client;
        }

        public AdmissionOperations Operations => AdmissionOperations.Create | AdmissionOperations.Update;

        public Task<ValidationResult> CreateAsync(HostedDatabase newEntity, bool _)
            => CheckIfHostExists(newEntity.Spec.Host);

        public Task<ValidationResult> UpdateAsync(HostedDatabase _, HostedDatabase newEntity, bool __)
            => CheckIfHostExists(newEntity.Spec.Host);

        public Task<ValidationResult> CreateAsync(DanglingDatabase newEntity, bool _)
            => CheckIfHostExists(newEntity.Spec.OriginalDatabase.Spec.Host);

        public Task<ValidationResult> UpdateAsync(
            DanglingDatabase _,
            DanglingDatabase newEntity,
            bool __)
            => CheckIfHostExists(newEntity.Spec.OriginalDatabase.Spec.Host);

        private async Task<ValidationResult> CheckIfHostExists(string hostname)
        {
            _logger.LogDebug(@"Check if host ""{host}"" exists on the cluster.", hostname);

            return await _client.Get<ClusterDatabaseHost>(hostname) == null
                ? ValidationResult.Fail(
                    StatusCodes.Status404NotFound,
                    $@"Database host ""{hostname}"" not found on the cluster.")
                : ValidationResult.Success();
        }
    }
}
