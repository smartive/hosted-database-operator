using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Entities;
using KubeOps.Operator.Webhooks;

namespace HostedDatabaseOperator.Webhooks
{
    public class CustomDbSettingsMutator : IMutationWebhook<HostedDatabase>
    {
        private readonly DatabaseConnectionPool _pool;

        public CustomDbSettingsMutator(DatabaseConnectionPool pool)
        {
            _pool = pool;
        }

        public AdmissionOperations Operations => AdmissionOperations.Create | AdmissionOperations.Update;

        public MutationResult Create(HostedDatabase newEntity, bool _)
            => CorrectFieldsIfNeeded(newEntity);

        public MutationResult Update(HostedDatabase _, HostedDatabase newEntity, bool __)
            => CorrectFieldsIfNeeded(newEntity);

        private MutationResult CorrectFieldsIfNeeded(HostedDatabase entity)
        {
            if (entity.Spec.DatabaseName == null && entity.Spec.Username == null)
            {
                return MutationResult.NoChanges();
            }

            var changes = false;
            var host = _pool.GetHost(entity.Spec.Host);

            if (entity.Spec.DatabaseName != null)
            {
                var newName = host.FormatDatabaseName(entity.Spec.DatabaseName);
                if (newName != entity.Spec.DatabaseName)
                {
                    changes = true;
                    entity.Spec.DatabaseName = newName;
                }
            }

            if (entity.Spec.Username != null)
            {
                var newName = host.FormatUsername(entity.Spec.Username);
                if (newName != entity.Spec.Username)
                {
                    changes = true;
                    entity.Spec.Username = newName;
                }
            }

            return changes
                ? MutationResult.Modified(
                    entity,
                    "The username or the database name was modified to match db specifications.")
                : MutationResult.NoChanges();
        }
    }
}
