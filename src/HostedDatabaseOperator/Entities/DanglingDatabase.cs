using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
using KubeOps.Operator.Rbac;

namespace HostedDatabaseOperator.Entities
{
    [Description("Describes the dangling database specification.")]
    public class DanglingDatabaseSpec
    {
        [Description("The name of the host to be used.")]
        [Required]
        [AdditionalPrinterColumn]
        public string Host { get; set; } = string.Empty;

        [Description("The name of the database (on the host).")]
        [Required]
        [AdditionalPrinterColumn]
        public string Name { get; set; } = string.Empty;

        [Description(
            "A reference to the secret that contains the credentials for the dangling database.")]
        [Required]
        public V1SecretReference CredentialsSecret { get; set; } = new();
    }

    [KubernetesEntity(Group = "hdo.smartive.ch", ApiVersion = "v2")]
    [EntityRbac(typeof(DanglingDatabase), Verbs = RbacVerb.All)]
    public class DanglingDatabase : CustomKubernetesEntity<DanglingDatabaseSpec>
    {
    }
}
