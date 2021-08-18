using System;
using HostedDatabaseOperator.Database;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

namespace HostedDatabaseOperator.Entities
{
    [Description("Configuration for a cluster database host.")]
    public class ClusterDatabaseHostSpec
    {
        [Description("Determines the type of the database host.")]
        [Required]
        public DatabaseType Type { get; set; }

        [Description("The hostname of the database.")]
        [Required]
        public string Host { get; set; } = string.Empty;

        [Description("Port to connect.")]
        [Required]
        public int Port { get; set; }

        [Description("The name of the secret that contains the connection information for this host.")]
        [Required]
        public string SecretName { get; set; } = string.Empty;

        [Description("The namespace of the secret that contains the connection information for this host.")]
        [Required]
        public string SecretNamespace { get; set; } = string.Empty;

        [Description("The name of the secret data key that contains the username for the connection.")]
        [Required]
        public string UsernameKey { get; set; } = string.Empty;

        [Description("The name of the secret data key that contains the password for the connection.")]
        [Required]
        public string PasswordKey { get; set; } = string.Empty;
    }

    [Description("Status information about the cluster database.")]
    public class ClusterDatabaseHostStatus
    {
        [Description("Determines the state if the host can be connected to.")]
        public bool Connected { get; set; }

        [Description("The timestamp of the last connection check.")]
        public DateTime? LastConnectionTest { get; set; }

        [Description("If given, the error message for the connection attempt.")]
        public string? Error { get; set; }
    }

    [KubernetesEntity(Group = "hdo.smartive.ch", ApiVersion = "v1")]
    [EntityScope(EntityScope.Cluster)]
    public class ClusterDatabaseHost : CustomKubernetesEntity<ClusterDatabaseHostSpec, ClusterDatabaseHostStatus>
    {
    }
}
