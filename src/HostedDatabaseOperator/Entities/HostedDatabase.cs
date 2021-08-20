using HostedDatabaseOperator.Database;
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

        [Description(
            "The action that is performed when a database is deleted. Defaults to 'create dangling database'.")]
        public DatabaseOnDeleteAction OnDelete { get; set; } = DatabaseOnDeleteAction.CreateDanglingDatabase;
    }

    [Description("Status of the hosted database.")]
    public class HostedDatabaseStatus
    {
        [Description("Name that is used on the host.")]
        [AdditionalPrinterColumn]
        public string? DbName { get; set; }

        [Description("Reference to the secret that contains the connection credentials.")]
        public V1SecretReference? Credentials { get; set; }

        [Description("Any error that might occur.")]
        public string? Error { get; set; }
    }

    [KubernetesEntity(Group = "hdo.smartive.ch", ApiVersion = "v2")]
    public class HostedDatabase : CustomKubernetesEntity<HostedDatabaseSpec, HostedDatabaseStatus>
    {
    }
}
