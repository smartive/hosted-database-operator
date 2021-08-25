using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotnetKubernetesClient;
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
    [EntityRbac(typeof(V1Secret), Verbs = RbacVerb.Get | RbacVerb.Update | RbacVerb.Delete)]
    public class DatabaseController : IResourceController<HostedDatabase>, IResourceController<DanglingDatabase>
    {
        private readonly ILogger<DatabaseController> _logger;
        private readonly IKubernetesClient _client;
        private readonly DatabaseConnectionPool _pool;
        private readonly OperatorSettings _settings;
        private readonly IFinalizerManager<HostedDatabase> _finalizerManager;
        private readonly IEventManager.AsyncMessagePublisher _processError;

        public DatabaseController(
            ILogger<DatabaseController> logger,
            IKubernetesClient client,
            DatabaseConnectionPool pool,
            OperatorSettings settings,
            IFinalizerManager<HostedDatabase> finalizerManager,
            IEventManager eventManager)
        {
            _logger = logger;
            _client = client;
            _pool = pool;
            _settings = settings;
            _finalizerManager = finalizerManager;
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
                    await _finalizerManager.RegisterFinalizerAsync<DeleteDatabaseFinalizer>(entity);
                    break;
                case DatabaseOnDeleteAction.CreateDanglingDatabase:
                    await _finalizerManager.RegisterFinalizerAsync<SoftDeleteDatabaseFinalizer>(entity);
                    break;
            }

            await ProcessDatabase(entity);
            return null;
        }

        private async Task ProcessDatabase(HostedDatabase database)
        {
            try
            {
                // TODO: check if the last database (in the status field)
                // is already present. if yes -> delete the old db or create dangling db.

                await using var host = _pool.GetHost(database.Spec.Host);
                var credentialsName = database.Spec.SecretName ?? $"{database.Name()}-credentials";
                var secret = await _client.Get<V1Secret>(credentialsName, database.Namespace()) ??
                             await CreateDatabaseSecret(credentialsName, database, host);
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
            HostedDatabase database,
            IDatabaseHost host)
        {
            var secret = new V1Secret().Initialize();

            var dbName = host.FormatDatabaseName(database.Spec.DatabaseName ?? database.Name());

            secret.Metadata.Name = secretName;
            secret.Metadata.SetNamespace(database.Namespace());
            secret.AddOwnerReference(database.MakeOwnerReference());
            secret.SetLabel("managed-by", _settings.Name);
            secret.SetLabel("database-instance", dbName);

            secret.WriteData("username", host.FormatUsername(database.Spec.Username ?? database.Name()));
            secret.WriteData("password", host.GeneratePassword());
            secret.WriteData("host", host.ConnectionConfiguration.Host);
            secret.WriteData("port", host.ConnectionConfiguration.Port.ToString());
            secret.WriteData("database", dbName);
            secret.WriteData("connection-string", host.ConnectionString(dbName));

            return await _client.Create(secret);
        }
    }
}
