using System;
using DotnetKubernetesClient.Entities;
using HostedDatabaseOperator.Database;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
using KubeOps.Operator.Rbac;

namespace HostedDatabaseOperator.Entities
{
    // TODO: what if - we use MysqlHost and PostgresHost to set db specific options like "version"?
    [Description("Configuration for a cluster database host.")]
    public class ClusterDatabaseHostSpec
    {
        [Description("Determines the type of the database host.")]
        [Required]
        public DatabaseType Type { get; set; }

        [Description("The hostname of the database.")]
        [Required]
        [AdditionalPrinterColumn]
        public string Host { get; set; } = string.Empty;

        [Description("Port to connect.")]
        [Required]
        public int Port { get; set; }

        [Description(
            "A reference to a secret that contains a username and a password to connect to this database host.")]
        [Required]
        public V1SecretReference CredentialsSecret { get; set; } = new();

        [Description(
            "The name of the secret data key that contains the username for the connection (defaults to 'username').")]
        public string UsernameKey { get; set; } = "username";

        [Description(
            "The name of the secret data key that contains the password for the connection (defaults to 'password').")]
        public string PasswordKey { get; set; } = "password";
    }

    [Description("Status information about the cluster database.")]
    public class ClusterDatabaseHostStatus
    {
        [Description("Determines the state if the host can be connected to.")]
        [AdditionalPrinterColumn]
        public bool Connected { get; set; }

        [Description("The timestamp of the last connection check.")]
        public DateTime? LastConnectionCheck { get; set; }

        [Description("If given, the error message for the connection attempt.")]
        public string? Error { get; set; }
    }

    [KubernetesEntity(Group = "hdo.smartive.ch", ApiVersion = "v2")]
    [EntityScope(EntityScope.Cluster)]
    [EntityRbac(typeof(ClusterDatabaseHost), Verbs = RbacVerb.Get | RbacVerb.Watch | RbacVerb.Update)]
    public class ClusterDatabaseHost : CustomKubernetesEntity<ClusterDatabaseHostSpec, ClusterDatabaseHostStatus>
    {
    }
}
