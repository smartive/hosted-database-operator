using System;
using System.ComponentModel.DataAnnotations;
using HostedDatabaseOperator.Database;
using k8s.Models;
using KubeOps.Operator.Entities;

namespace HostedDatabaseOperator.Entities
{
    [Display(Description = "Configuration for a cluster database host.")]
    public class ClusterDatabaseHostSpec
    {
        [Display(Description = "Determines the type of the database host.")]
        public DatabaseType Type { get; set; }

        [Display(Description = "The hostname of the database.")]
        public string Host { get; set; } = string.Empty;

        [Display(Description = "Port to connect.")]
        public int Port { get; set; }

        [Display(Description = "The name of the secret that contains the connection information for this host.")]
        public string SecretName { get; set; } = string.Empty;

        [Display(Description = "The namespace of the secret that contains the connection information for this host.")]
        public string SecretNamespace { get; set; } = string.Empty;

        [Display(Description = "The name of the secret data key that contains the username for the connection.")]
        public string UsernameKey { get; set; } = string.Empty;

        [Display(Description = "The name of the secret data key that contains the password for the connection.")]
        public string PasswordKey { get; set; } = string.Empty;
    }

    [Display(Description = "Status information about the cluster database.")]
    public class ClusterDatabaseHostStatus
    {
        [Display(Description = "Determines the state if the host can be connected to.")]
        public bool Connected { get; set; }

        [Display(Description = "The timestamp of the last connection check.")]
        public DateTime? LastConnectionTest { get; set; }

        [Display(Description = "If given, the error message for the connection attempt.")]
        public string? Error { get; set; }
    }

    [KubernetesEntity(Group = "hdo.smartive.ch", ApiVersion = "v1")]
    [EntityScope(EntityScope.Cluster)]
    [Display(Description = "ClusterDatabaseHost is the Schema for the clusterdatabasehosts API")]
    public class ClusterDatabaseHost : CustomKubernetesEntity<ClusterDatabaseHostSpec, ClusterDatabaseHostStatus>
    {
    }
}
