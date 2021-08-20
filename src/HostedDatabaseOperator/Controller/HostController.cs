using System;
using System.Threading.Tasks;
using DotnetKubernetesClient;
using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Entities;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Entities.Extensions;
using KubeOps.Operator.Rbac;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Controller
{
    [EntityRbac(typeof(V1Secret), Verbs = RbacVerb.All)]
    public class HostController : IResourceController<ClusterDatabaseHost>
    {
        private readonly ILogger<HostController> _logger;
        private readonly DatabaseConnectionsPool _pool;
        private readonly IKubernetesClient _client;

        public HostController(
            ILogger<HostController> logger,
            DatabaseConnectionsPool pool,
            IKubernetesClient client)
        {
            _logger = logger;
            _pool = pool;
            _client = client;
        }

        public async Task<ResourceControllerResult?> CreatedAsync(ClusterDatabaseHost entity)
        {
            await UpdateDatabaseHost(entity);
            return ResourceControllerResult.RequeueEvent(TimeSpan.FromMinutes(1));
        }

        public async Task<ResourceControllerResult?> UpdatedAsync(ClusterDatabaseHost entity)
        {
            await UpdateDatabaseHost(entity);
            return ResourceControllerResult.RequeueEvent(TimeSpan.FromMinutes(1));
        }

        public async Task<ResourceControllerResult?> NotModifiedAsync(ClusterDatabaseHost entity)
        {
            await CheckConnection(entity);
            return ResourceControllerResult.RequeueEvent(TimeSpan.FromMinutes(1));
        }

        public Task DeletedAsync(ClusterDatabaseHost entity)
        {
            _pool.Remove(entity.Metadata.Name);
            _logger.LogInformation(
                @"Cluster database host ""{name}"" was deleted. Removed the connection.",
                entity.Name());
            return Task.CompletedTask;
        }

        private async Task UpdateDatabaseHost(ClusterDatabaseHost host)
        {
            _logger.LogDebug(
                @"Cluster database host ""{name}"" was modified. Update connection.",
                host.Name());

            var spec = host.Spec;
            var secret =
                await _client.Get<V1Secret>(spec.CredentialsSecret.Name, spec.CredentialsSecret.NamespaceProperty);
            if (secret == null)
            {
                await ConnectionError(
                    host,
                    $@"Secret with name ""{spec.CredentialsSecret.Name}"" in namespace ""{spec.CredentialsSecret.NamespaceProperty}"" not found.");
                return;
            }

            if (!secret.Data.ContainsKey(spec.UsernameKey))
            {
                await ConnectionError(
                    host,
                    $@"Secret with name ""{spec.CredentialsSecret.Name}"" contains no username key ""{spec.UsernameKey}"".");
                return;
            }

            if (!secret.Data.ContainsKey(spec.PasswordKey))
            {
                await ConnectionError(
                    host,
                    $@"Secret with name ""{spec.CredentialsSecret.Name}"" contains no password key ""{spec.PasswordKey}"".");
                return;
            }

            var user = secret.ReadData(spec.UsernameKey);
            var pass = secret.ReadData(spec.PasswordKey);

            _pool.Add(host.Name(), new(spec.Type, spec.Host, Convert.ToInt16(spec.Port), user, pass));

            _logger.LogInformation(
                @"Cluster database host ""{name}"" was modified. Updated connection.",
                host.Metadata.Name);

            await CheckConnection(host);
        }

        private async Task CheckConnection(ClusterDatabaseHost host)
        {
            _logger.LogDebug(
                @"Cluster database host ""{name}"" connection check.",
                host.Name());
            host.Status.LastConnectionCheck = DateTime.UtcNow;

            try
            {
                await using var database = _pool.GetHost(host.Name());
                await database.CanConnect();
                host.Status.Connected = true;
                host.Status.Error = null;
            }
            catch (Exception e)
            {
                host.Status.Connected = false;
                host.Status.Error = e.Message;
            }
            finally
            {
                try
                {
                    await _client.UpdateStatus(host);
                }
                catch (Exception e)
                {
                    _logger.LogError(
                        e,
                        @"Could not update status on resource ""{kind}/{name}"".",
                        host.Kind,
                        host.Name());
                }
            }
        }

        private async Task ConnectionError(ClusterDatabaseHost host, string reason)
        {
            host.Status.Connected = false;
            host.Status.LastConnectionCheck = DateTime.UtcNow;
            host.Status.Error = reason;
            await _client.UpdateStatus(host);
        }
    }
}
