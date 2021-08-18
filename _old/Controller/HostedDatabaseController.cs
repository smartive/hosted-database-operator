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

                var configMap = await Client.Get<V1ConfigMap>(configMapName, @namespace);

                if (configMap == null)
                {
                    configMap = new V1ConfigMap(
                        V1ConfigMap.KubeApiVersion,
                        kind: V1ConfigMap.KubeKind,
                        metadata: new V1ObjectMeta
                        {
                            Name = configMapName,
                            NamespaceProperty = @namespace,
                            OwnerReferences = new List<V1OwnerReference>
                            {
                                new V1OwnerReference(resource.ApiVersion, resource.Kind, resource.Metadata.Name,
                                    resource.Metadata.Uid, false, true),
                            }
                        });

                    configMap.Data = new Dictionary<string, string>
                    {
                        ["host"] = host.Config.Host,
                        ["port"] = host.Config.Port.ToString(),
                        ["database"] = host.FormatDatabaseName(resource.Metadata.Name),
                    };

                    configMap.Metadata.Labels = new Dictionary<string, string>
                    {
                        ["managed-by"] = "hosted-database-operator",
                        ["database-instance"] = host.FormatDatabaseName(resource.Metadata.Name),
                    };

                    configMap = await Client.Create(configMap);
                    _logger.LogDebug(
                        @"Create config map ""{configName}"" for database ""{database}"".",
                        configMap.Metadata.Name,
                        resource.Metadata.Name);
                }

                var secret = await Client.Get<V1Secret>(secretName, @namespace);
                if (secret == null)
                {
                    _logger.LogDebug(
                        @"Secret ""{name}"" did not exist. Create Secret and User.",
                        secretName);
                    secret = new V1Secret(
                        V1Secret.KubeApiVersion,
                        kind: V1Secret.KubeKind,
                        metadata: new V1ObjectMeta
                        {
                            Name = secretName,
                            NamespaceProperty = @namespace,
                            OwnerReferences = new List<V1OwnerReference>
                            {
                                new V1OwnerReference(resource.ApiVersion, resource.Kind, resource.Metadata.Name,
                                    resource.Metadata.Uid, false, true),
                            }
                        });

                    secret.Data = new Dictionary<string, byte[]>
                    {
                        ["username"] = Encoding.UTF8.GetBytes(host.FormatUsername(resource.Metadata.Name)),
                    };

                    secret.Metadata.Labels = new Dictionary<string, string>
                    {
                        ["managed-by"] = "hosted-database-operator",
                        ["database-instance"] = host.FormatDatabaseName(resource.Metadata.Name),
                    };

                    secret = await Client.Create(secret);
                }

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
