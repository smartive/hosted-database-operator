using k8s.Models;
using KubeOps.Operator.Entities;

namespace HostedDatabaseOperator.Entities
{
    public class HostedDatabaseSpec
    {
        public string Host { get; set; } = string.Empty;
    }

    public class HostedDatabaseStatus
    {
        public string? DbName { get; set; }

        public string? DbHost { get; set; }

        public string? ConfigMapName { get; set; }

        public string? SecretName { get; set; }

        public string? Error { get; set; }
    }

    [KubernetesEntity(Group = "hdo.smartive.ch", ApiVersion = "v1")]
    public class HostedDatabase : CustomKubernetesEntity<HostedDatabaseSpec, HostedDatabaseStatus>
    {
    }
}
