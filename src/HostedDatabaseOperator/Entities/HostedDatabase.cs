using HostedDatabaseOperator.Database;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

namespace HostedDatabaseOperator.Entities
{
    /// <summary>
    /// Describes the hosted database specification.
    /// </summary>
    public class HostedDatabaseSpec
    {
        /// <summary>
        /// The name of the host to be used.
        /// This name must refer to a <see cref="ClusterDatabaseHost"/>,
        /// otherwise, the operator cannot create the database.
        /// </summary>
        [Required]
        [AdditionalPrinterColumn]
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// If set, overwrites the name of the database to be used.
        /// If omitted, the operator defaults to the formatted name
        /// of the <see cref="HostedDatabase"/>.
        /// </summary>
        public string? DatabaseName { get; set; }

        /// <summary>
        /// If set, overwrites the name of the user for the database.
        /// If omitted, the operator defaults to the formatted name
        /// of the <see cref="HostedDatabase"/>.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// If set, overwrites the name of the secret that stores the
        /// connection information for the database. If omitted, the operator
        /// defaults to "formatted-db-name" with the suffix "-credentials".
        /// </summary>
        public string? SecretName { get; set; }

        /// <summary>
        /// The action that is performed when a database is deleted.
        /// Defaults to "CreateDanglingDatabase". DeleteDatabase
        /// will directly delete the database on the host if the entity
        /// is deleted in Kubernetes.
        /// Can be one of: "CreateDanglingDatabase", "DeleteDatabase".
        /// </summary>
        public DatabaseOnDeleteAction OnDelete { get; set; } = DatabaseOnDeleteAction.CreateDanglingDatabase;
    }

    /// <summary>
    /// Status of the hosted database.
    /// </summary>
    public class HostedDatabaseStatus
    {
        /// <summary>
        /// Computed name of the database. This represents the
        /// name of the database on the host.
        /// </summary>
        [AdditionalPrinterColumn]
        public string? DbName { get; set; }

        /// <summary>
        /// Reference to the secret that contains the connection credentials.
        /// </summary>
        public V1SecretReference? Credentials { get; set; }
    }

    /// <summary>
    /// Hosted Database on a Kubernetes cluster. This entity creates
    /// a database on a (self-)managed database host.
    /// </summary>
    [KubernetesEntity(Group = "hdo.smartive.ch", ApiVersion = "v2")]
    public class HostedDatabase : CustomKubernetesEntity<HostedDatabaseSpec, HostedDatabaseStatus>
    {
    }
}
