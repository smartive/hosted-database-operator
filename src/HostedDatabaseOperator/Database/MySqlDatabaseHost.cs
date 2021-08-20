using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace HostedDatabaseOperator.Database
{
    public class MySqlDatabaseHost : IDatabaseHost
    {
        private readonly ConnectionConfiguration _config;
        private readonly ILogger<MySqlDatabaseHost> _logger;
        private readonly MySqlConnection _connection;

        public MySqlDatabaseHost(ConnectionConfiguration config, ILogger<MySqlDatabaseHost> logger)
        {
            _connection =
                new MySqlConnection(
                    $"server={config.Host};port={config.Port};user={config.Username};password={config.Password};");
            _config = config;
            _logger = logger;
        }

        public async Task<bool> CanConnect()
        {
            _logger.LogDebug(
                @"Check connection to database ""{host}"" on port ""{port}"".",
                _config.Host,
                _config.Port);

            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            return _connection.Ping();
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}
