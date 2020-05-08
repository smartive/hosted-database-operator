using System;
using System.Threading.Tasks;
using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Entities;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.KubernetesEntities;
using KubeOps.Operator.Rbac;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Controller
{
    [EntityRbac(typeof(ClusterDatabaseHost), Verbs = RbacVerb.All)]
    [EntityRbac(typeof(V1Secret), Verbs = RbacVerb.Get)]
    public class ClusterDatabaseHostController : ResourceControllerBase<ClusterDatabaseHost>
    {
        private readonly ILogger<ClusterDatabaseHostController> _logger;

        public ClusterDatabaseHostController(
            ILogger<ClusterDatabaseHostController> logger)
        {
            _logger = logger;
        }

        protected override async Task<TimeSpan?> Created(ClusterDatabaseHost resource)
        {
            _logger.LogInformation(
                @"Cluster database host ""{name}"" was created. Add to configs.",
                resource.Metadata.Name);
            await UpdateDatabaseConfigs(resource);
            return TimeSpan.FromMinutes(1);
        }

        protected override async Task<TimeSpan?> Updated(ClusterDatabaseHost resource)
        {
            _logger.LogInformation(
                @"Cluster database host ""{name}"" was updated. Update in configs.",
                resource.Metadata.Name);
            await UpdateDatabaseConfigs(resource);
            return TimeSpan.FromMinutes(1);
        }

        protected override async Task<TimeSpan?> NotModified(ClusterDatabaseHost resource)
        {
            await CheckConnectivity(resource);
            return TimeSpan.FromMinutes(1);
        }

        protected override Task Deleted(ClusterDatabaseHost resource)
        {
            _logger.LogInformation(
                @"Cluster database host ""{name}"" was deleted. Remove the configuration.",
                resource.Metadata.Name);
            ConnectionsManager.Remove(resource.Metadata.Name);
            return Task.CompletedTask;
        }

        private async Task UpdateDatabaseConfigs(ClusterDatabaseHost resource)
        {
            var spec = resource.Spec;
            var secret = await Client.Get<V1Secret>(spec.SecretName, spec.SecretNamespace);
            if (secret == null)
            {
                resource.Status.Connected = false;
                resource.Status.Error =
                    $@"Secret with name ""{spec.SecretName}"" in namespace ""{spec.SecretNamespace}"" not found.";
                await Client.UpdateStatus(resource);
                return;
            }

            var user = secret.ReadData(spec.UsernameKey);
            var pass = secret.ReadData(spec.PasswordKey);

            ConnectionsManager.Add(
                resource.Metadata.Name,
                new ConnectionConfig
                {
                    Type = spec.Type,
                    Host = spec.Host,
                    Port = spec.Port,
                    Username = user,
                    Password = pass,
                });

            await CheckConnectivity(resource);
        }

        private async Task CheckConnectivity(ClusterDatabaseHost resource)
        {
            _logger.LogDebug(
                @"Cluster database host ""{name}"" connection check.",
                resource.Metadata.Name);
            resource.Status.LastConnectionTest = DateTime.UtcNow;
            await using var host = ConnectionsManager.GetHost(resource.Metadata.Name);
            try
            {
                await host.CanConnect();
                resource.Status.Connected = true;
                resource.Status.Error = null;
            }
            catch (Exception e)
            {
                resource.Status.Connected = false;
                resource.Status.Error = e.Message;
            }
            finally
            {
                try
                {
                    await Client.UpdateStatus(resource);
                }
                catch (Exception e)
                {
                    _logger.LogError(
                        e,
                        @"Could not update status on resource ""{kind}/{name}"".",
                        resource.Kind,
                        resource.Metadata.Name);
                }
            }
        }
    }
}
