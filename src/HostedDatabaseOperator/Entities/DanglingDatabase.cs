using DotnetKubernetesClient.Entities;
using HostedDatabaseOperator.Database;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
using KubeOps.Operator.Rbac;

namespace HostedDatabaseOperator.Entities
{
    /// <summary>
    /// Describes the configuration of a dangling database.
    /// </summary>
    public class DanglingDatabaseSpec
    {
        /// <summary>
        /// A <see cref="HostedDatabase"/> object. This is the object that
        /// originally created the dangling database.
        /// </summary>
        [Required]
        [EmbeddedResource]
        public HostedDatabase OriginalDatabase { get; set; } = new();
    }

    /// <summary>
    /// Dangling database. This entity should never be created by a user.
    /// If a <see cref="HostedDatabase"/> is deleted with the <see cref="HostedDatabaseSpec.OnDelete"/>
    /// setting of <see cref="DatabaseOnDeleteAction.CreateDanglingDatabase"/>, the
    /// operator creates a <see cref="DanglingDatabase"/> without actually deleting the database on the host.
    /// This can be used as "backup" when entities are moved or other use cases.
    /// When the <see cref="DanglingDatabase"/> is deleted, the effective database
    /// is removed.
    /// </summary>
    [KubernetesEntity(Group = "hdo.smartive.ch", ApiVersion = "v2")]
    public class DanglingDatabase : CustomKubernetesEntity<DanglingDatabaseSpec>
    {
        public DanglingDatabase()
        {
            var crd = this.CreateResourceDefinition();
            Kind = crd.Kind;
            ApiVersion = $"{crd.Group}/{crd.Version}";
        }
    }
}
