using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Entities;
using HostedDatabaseOperator.Finalizer;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Entities.Extensions;
using KubeOps.Operator.Rbac;
using KubeOps.Operator.Services;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Controller
{
    [EntityRbac(typeof(HostedDatabase), Verbs = RbacVerb.All)]
    [EntityRbac(
        typeof(V1Secret),
        typeof(V1ConfigMap),
        Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Create | RbacVerb.Update | RbacVerb.Delete)]
    public class HostedDatabaseController : ResourceControllerBase<HostedDatabase>
    {
        private readonly ILogger<HostedDatabaseController> _logger;
        private readonly ConnectionsManager _connectionsManager;

        public HostedDatabaseController(
            ILogger<HostedDatabaseController> logger,
            ConnectionsManager connectionsManager,
            IResourceServices<HostedDatabase> services)
            : base(services)
        {
            _logger = logger;
            _connectionsManager = connectionsManager;
        }

        protected override async Task<TimeSpan?> Created(HostedDatabase resource)
        {
            _logger.LogDebug(
                @"Hosted Database ""{name}"" was created. Check and create Database.",
                resource.Metadata.Name);
            await AttachFinalizer<HostedDatabaseFinalizer>(resource);
            await CheckDatabase(resource);
            return null;
        }

        private async Task CheckDatabase(HostedDatabase resource)
        {
            try
            {
                await using var host = _connectionsManager.GetHost(resource.Spec.Host);
                var @namespace = resource.Metadata.NamespaceProperty;
                var secretName = $"{resource.Metadata.Name}-auth";
                var configMapName = $"{resource.Metadata.Name}-config";


                var db = configMap.Data["database"];
                resource.Status.DbHost = $"{host.Config.Host}:{host.Config.Port}";
                resource.Status.DbName ??= db;
                resource.Status.SecretName ??= secretName;
                resource.Status.ConfigMapName ??= configMapName;

                var user = secret.ReadData("username");

                var password = await host.ProcessDatabase(db, user);

                if (password != null)
                {
                    _logger.LogInformation(
                        @"User ""{user}"" for database ""{database}"" updated password.",
                        user,
                        resource.Metadata.Name);
                    secret.WriteData("password", password);
                    await Client.Update(secret);
                }

                resource.Status.Error = null;
            }
            catch (Exception e)
            {
                resource.Status.Error = e.Message;
                _logger.LogError(
                    e,
                    @"Could not create / update / check database ""{database}"".",
                    resource.Metadata.Name);
                throw;
            }
            finally
            {
                await Client.UpdateStatus(resource);
            }
        }
    }
}
