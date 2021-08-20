using DotnetKubernetesClient;
using HostedDatabaseOperator.Database;
using HostedDatabaseOperator.Entities;
using KubeOps.Operator.Controller;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Controller
{
    public class DatabaseController : IResourceController<HostedDatabase>, IResourceController<DanglingDatabase>
    {
        private readonly ILogger<DatabaseController> _logger;
        private readonly IKubernetesClient _client;
        private readonly DatabaseConnectionsPool _pool;

        public DatabaseController(
            ILogger<DatabaseController> logger,
            IKubernetesClient client,
            DatabaseConnectionsPool pool)
        {
            _logger = logger;
            _client = client;
            _pool = pool;
        }


    }
}
