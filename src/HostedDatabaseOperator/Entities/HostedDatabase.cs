using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

namespace HostedDatabaseOperator.Entities
{
    [Description("Describes the hosted database specification.")]
    public class HostedDatabaseSpec
    {
        [Description("The name of the host to be used.")]
        [Required]
        public string Host { get; set; } = string.Empty;
    }

    [Description("Status of the hosted database.")]
    public class HostedDatabaseStatus
    {
        [Description("Name that is used on the host.")]
        public string? DbName { get; set; }

        [Description("Host name of the database.")]
        public string? DbHost { get; set; }

        [Description("Name of the config map that contains the host / port / name.")]
        public string? ConfigMapName { get; set; }

        [Description("Name of the secret in the namespace that contains username / password.")]
        public string? SecretName { get; set; }

        [Description("Any error that might occur.")]
        public string? Error { get; set; }
    }

    [KubernetesEntity(Group = "hdo.smartive.ch", ApiVersion = "v1")]
    public class HostedDatabase : CustomKubernetesEntity<HostedDatabaseSpec, HostedDatabaseStatus>
    {
    }
}
