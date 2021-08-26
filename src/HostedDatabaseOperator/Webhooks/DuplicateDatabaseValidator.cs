using System.Linq;
using System.Threading.Tasks;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Entities;
using k8s.Models;
using KubeOps.Operator.Rbac;
using KubeOps.Operator.Webhooks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Webhooks
{
    [EntityRbac(typeof(DanglingDatabase), Verbs = RbacVerb.List)]
    public class DuplicateDatabaseValidator : IValidationWebhook<HostedDatabase>
    {
        private readonly ILogger<DuplicateDatabaseValidator> _logger;
        private readonly DatabaseConnectionPool _pool;
        private readonly IKubernetesClient _client;

        public DuplicateDatabaseValidator(
            ILogger<DuplicateDatabaseValidator> logger,
            DatabaseConnectionPool pool,
            IKubernetesClient client)
        {
            _logger = logger;
            _pool = pool;
            _client = client;
        }

        public AdmissionOperations Operations => AdmissionOperations.Create | AdmissionOperations.Update;

        public Task<ValidationResult> CreateAsync(HostedDatabase newEntity, bool _)
            => CheckIfDbExists(newEntity);

        public Task<ValidationResult> UpdateAsync(HostedDatabase _, HostedDatabase newEntity, bool __)
            => CheckIfDbExists(newEntity);

        private async Task<ValidationResult> CheckIfDbExists(HostedDatabase entity)
        {
            /*
             * This checks if a database on the given host already exists.
             * IF there is a dangling database for the given database,
             * the validator returns success, since the hosted database
             * can be created "back".
             */

            await using var host = _pool.GetHost(entity.Spec.Host);
            var database = host.FormatDatabaseName(entity.Spec.DatabaseName ?? entity.Name());

            _logger.LogDebug(
                @"Check if database ""{database}"" already exists on host ""{host}"".",
                database,
                entity.Spec.Host);

            if (await host.DatabaseExists(database))
            {
                _logger.LogTrace(
                    @"Database ""{database}"" already exists on host ""{host}"". Checking for dangling database.",
                    database,
                    entity.Spec.Host);

                var danglingDb = (await _client.List<DanglingDatabase>(
                    labelSelectors: new EqualsSelector("hdo.smartive.ch/database-name", database))).SingleOrDefault();
                // if "single or default" throws an exception, there are multiple dangling databases with the same
                // database name... this should not happen.

                _logger.LogInformation(
                    @"Database ""{database}"" already exists on host ""{host}"". A dangling database {danglingDbState}.",
                    database,
                    entity.Spec.Host,
                    danglingDb == null ? "does not exist" : "exists");

                return danglingDb == null
                    ? ValidationResult.Fail(
                        StatusCodes.Status409Conflict,
                        $@"A database ""{database}"" already exists on host ""{entity.Spec.Host}"", but no dangling database exists.")
                    : ValidationResult.Success(
                        $@"The database ""{database}"" already exists on ""{entity.Spec.Host}"" with a dangling database. The database may already contain data.");
            }

            return ValidationResult.Success();
        }
    }
}
