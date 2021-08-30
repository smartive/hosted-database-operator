using System;
using DotnetKubernetesClient.Entities;
using HostedDatabaseOperator.Database;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
using KubeOps.Operator.Rbac;

namespace HostedDatabaseOperator.Entities
{
    /// <summary>
    /// Configuration for a cluster database host.
    /// </summary>
    public class ClusterDatabaseHostSpec
    {
        /// <summary>
        /// Determines the type of the database host.
        /// Can be one of: "Postgres", "MySql".
        /// </summary>
        [Required]
        public DatabaseType Type { get; init; }

        /// <summary>
        /// The hostname of the database.
        /// </summary>
        [Required]
        [AdditionalPrinterColumn]
        public string Host { get; init; } = string.Empty;

        /// <summary>
        /// Port of the connection to the database host.
        /// </summary>
        [Required]
        public int Port { get; init; }

        /// <summary>
        /// The SSL mode for the connection. Defaults to Disabled.
        /// Can be one of "Disabled" and "Required".
        /// </summary>
        public SslMode SslMode { get; set; } = SslMode.Disabled;

        /// <summary>
        /// A reference to a secret that contains a username and a password to connect to the database host.
        /// </summary>
        [Required]
        public V1SecretReference CredentialsSecret { get; set; } = new();

        /// <summary>
        /// The name of the secret data key that contains
        /// the username for the connection (defaults to "username").
        /// </summary>
        public string UsernameKey { get; init; } = "username";

        /// <summary>
        /// The name of the secret data key that contains
        /// the password for the connection (defaults to "password").
        /// </summary>
        public string PasswordKey { get; init; } = "password";
    }

    /// <summary>
    /// Status information about the cluster database host.
    /// </summary>
    public class ClusterDatabaseHostStatus
    {
        /// <summary>
        /// Determines the state if the host can be connected to.
        /// If the connection-check returns an error, an event
        /// is created with the error message.
        /// </summary>
        [AdditionalPrinterColumn]
        public bool Connected { get; set; }

        /// <summary>
        /// To reduce event creation, the last connection check (timestamp)
        /// is stored in this status field.
        /// </summary>
        public DateTime? LastConnectionCheck { get; set; }
    }

    /// <summary>
    /// Cluster wide database host. Defines access to a (self-)managed
    /// database host where the operator can manage databases.
    /// </summary>
    [KubernetesEntity(Group = "hdo.smartive.ch", ApiVersion = "v2")]
    [EntityScope(EntityScope.Cluster)]
    public class ClusterDatabaseHost : CustomKubernetesEntity<ClusterDatabaseHostSpec, ClusterDatabaseHostStatus>
    {
    }
}
