using System;
using System.Linq;
using System.Threading.Tasks;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Entities;
using HostedDatabaseOperator.Finalizer;
using k8s;
using k8s.Models;
using KubeOps.Operator;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Entities.Extensions;
using KubeOps.Operator.Events;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Controller
{
    [EntityRbac(typeof(V1Secret), Verbs = RbacVerb.Get | RbacVerb.Create | RbacVerb.Update)]
    [EntityRbac(typeof(DanglingDatabase), Verbs = RbacVerb.List | RbacVerb.Update | RbacVerb.Delete)]
    [EntityRbac(typeof(HostedDatabase), Verbs = RbacVerb.Update | RbacVerb.Watch)]
    [EntityRbac(typeof(Corev1Event), Verbs = RbacVerb.Create)]
    public class DatabaseController : IResourceController<HostedDatabase>
    {
        private readonly ILogger<DatabaseController> _logger;
        private readonly IKubernetesClient _client;
        private readonly DatabaseConnectionPool _pool;
        private readonly OperatorSettings _settings;
        private readonly IFinalizerManager<HostedDatabase> _hostedFinalizer;
        private readonly IFinalizerManager<DanglingDatabase> _danglingFinalizer;
        private readonly IEventManager.AsyncMessagePublisher _processError;

        public DatabaseController(
            ILogger<DatabaseController> logger,
            IKubernetesClient client,
            DatabaseConnectionPool pool,
            OperatorSettings settings,
            IFinalizerManager<HostedDatabase> hostedFinalizer,
            IFinalizerManager<DanglingDatabase> danglingFinalizer,
            IEventManager eventManager)
        {
            _logger = logger;
            _client = client;
            _pool = pool;
            _settings = settings;
            _hostedFinalizer = hostedFinalizer;
            _danglingFinalizer = danglingFinalizer;
            _processError = eventManager.CreatePublisher("error_processing_database", EventType.Warning);
        }

        public async Task<ResourceControllerResult?> CreatedAsync(HostedDatabase entity)
        {
            _logger.LogDebug(
                @"Hosted Database for host ""{host}"" with name ""{name}"" was created. Check and create Database.",
                entity.Spec.Host,
                entity.Name());

            switch (entity.Spec.OnDelete)
            {
                case DatabaseOnDeleteAction.DeleteDatabase:
                    await _hostedFinalizer.RegisterFinalizerAsync<DeleteDatabaseFinalizer>(entity);
                    break;
                case DatabaseOnDeleteAction.CreateDanglingDatabase:
                    await _hostedFinalizer.RegisterFinalizerAsync<SoftDeleteDatabaseFinalizer>(entity);
                    break;
            }

            await ProcessDatabase(entity);
            return null;
        }

        private async Task ProcessDatabase(HostedDatabase database)
        {
            try
            {
                /*
                 * Check for a dangling database. if one exists,
                 * set the according variables and remove the dangling
                 * database.
                 */

                await using var host = _pool.GetHost(database.Spec.Host);

                var dbName = host.FormatDatabaseName(database.Spec.DatabaseName ?? database.Name());
                var credentialsName = database.Spec.SecretName ?? $"{database.Name()}-credentials";

                var danglingDb = (await _client.List<DanglingDatabase>(
                    labelSelectors: new EqualsSelector("hdo.smartive.ch/database-name", dbName))).SingleOrDefault();

                if (danglingDb != null)
                {
                    _logger.LogInformation(
                        @"Create database ""{database}"" for CRD with existing dangling database.",
                        dbName);

                    /*
                     * Set the database name of the hosted db and the credentials.
                     */
                    dbName = danglingDb.Spec.OriginalDatabase.Status.DbName ?? dbName;
                    credentialsName = danglingDb.Spec.OriginalDatabase.Status.Credentials?.Name ?? credentialsName;
                }

                var secret = await _client.Get<V1Secret>(credentialsName, database.Namespace()) ??
                             await CreateDatabaseSecret(credentialsName, dbName, database, host);

                if (danglingDb != null)
                {
                    secret.RemoveOwnerReference(danglingDb);
                    secret.AddOwnerReference(database.MakeOwnerReference());
                    await _client.Update(secret);
                    await _danglingFinalizer.RemoveFinalizerAsync<DeleteDatabaseFinalizer>(danglingDb);
                    await _client.Delete(danglingDb);
                }

                database.Status.DbName = secret.ReadData("database");
                database.Status.Credentials = new(secret.Name(), secret.Namespace());

                await host.UpsertDatabase(
                    database,
                    secret.ReadData("database"),
                    secret.ReadData("username"),
                    secret.ReadData("password"));
            }
            catch (Exception e)
            {
                await _processError(database, e.Message);
                _logger.LogError(
                    e,
                    @"Could not create / update / check database ""{database}"".",
                    database.Name());
                throw;
            }
            finally
            {
                await _client.UpdateStatus(database);
            }
        }

        private async Task<V1Secret> CreateDatabaseSecret(
            string secretName,
            string databaseName,
            HostedDatabase database,
            IDatabaseHost host)
        {
            var secret = new V1Secret().Initialize();

            var username = host.FormatUsername(database.Spec.Username ?? database.Name());

            secret.Metadata.Name = secretName;
            secret.Metadata.SetNamespace(database.Namespace());
            secret.AddOwnerReference(database.MakeOwnerReference());
            secret.SetLabel("managed-by", _settings.Name);
            secret.SetLabel("hdo.smartive.ch/database-name", databaseName);
            secret.SetLabel("hdo.smartive.ch/database-user", username);

            secret.WriteData("username", username);
            secret.WriteData("password", host.GeneratePassword());
            secret.WriteData("host", host.ConnectionConfiguration.Host);
            secret.WriteData("port", host.ConnectionConfiguration.Port.ToString());
            secret.WriteData("database", databaseName);
            secret.WriteData("connection-string", host.ConnectionString(databaseName));

            return await _client.Create(secret);
        }
    }
}
