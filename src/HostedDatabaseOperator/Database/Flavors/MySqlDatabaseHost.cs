using System.Data;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HostedDatabaseOperator.Entities;
using KubeOps.Operator.Events;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace HostedDatabaseOperator.Database.Flavors
{
    public class MySqlDatabaseHost : IDatabaseHost
    {
        private const byte DatabaseNameMaxLength = 64;
        private const byte UsernameMaxLength = 16;

        private static readonly Regex IllegalChars = new("[^a-zA-Z_-]");
        private static readonly Regex Dashes = new("[-]");

        private readonly ILogger<MySqlDatabaseHost> _logger;
        private readonly MySqlConnection _connection;
        private readonly IEventManager.AsyncPublisher _userCreated;
        private readonly IEventManager.AsyncPublisher _databaseCreated;

        public MySqlDatabaseHost(
            ConnectionConfiguration config,
            ILogger<MySqlDatabaseHost> logger,
            IEventManager eventManager)
        {
            ConnectionConfiguration = config;
            _connection =
                new(ConnectionString());
            _logger = logger;
            _userCreated = eventManager.CreatePublisher(
                "db_user_created",
                "The user for the database was successfully created.");
            _databaseCreated = eventManager.CreatePublisher(
                "db_created",
                "The database was successfully created.");
        }

        public ConnectionConfiguration ConnectionConfiguration { get; }

        public async Task<bool> CanConnect()
        {
            _logger.LogDebug(
                @"Check connection to database ""{host}"" on port ""{port}"".",
                ConnectionConfiguration.Host,
                ConnectionConfiguration.Port);

            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            return _connection.Ping();
        }

        public string ConnectionString(string? databaseName = null) =>
            $"server={ConnectionConfiguration.Host};port={ConnectionConfiguration.Port};" +
            $"user={ConnectionConfiguration.Username};password={ConnectionConfiguration.Password};" +
            $"{(string.IsNullOrWhiteSpace(databaseName) ? string.Empty : $"database={databaseName};")}";

        public string FormatDatabaseName(string name)
        {
            var str = Dashes.Replace(IllegalChars.Replace(name, string.Empty), "_");
            return str.Length > DatabaseNameMaxLength
                ? str[..DatabaseNameMaxLength]
                : str;
        }

        public string FormatUsername(string name)
        {
            var str = IllegalChars.Replace(name, string.Empty);
            return str.Length > UsernameMaxLength
                ? str[..UsernameMaxLength]
                : str;
        }

        public async Task UpsertDatabase(HostedDatabase entity, string dbName, string username, string password)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            /*
             * Create the database IF it does not exist
             * Create the user IF it does not exist (with the given password)
             * Make user owner of the given database.
             */

            if (!await DatabaseExists(dbName))
            {
                _logger.LogInformation(
                    @"Hosted Database ""{name}"" did not exist. Create Database on host ""{host}"".",
                    dbName,
                    ConnectionConfiguration.Host);
                await CreateDatabase(dbName);
                await _databaseCreated(entity);
            }

            if (!await UserExists(username))
            {
                _logger.LogInformation(
                    @"User does not exist, create user ""{user}"" on ""{database}"".",
                    username,
                    dbName);
                await CreateUser(username, password);
                await _userCreated(entity);
            }

            if (!await UserHasAccess(username, dbName))
            {
                _logger.LogInformation(
                    @"User has no access. Grant access to ""{user}"" on ""{database}"".",
                    username,
                    dbName);
                await AttachUserToDatabase(username, dbName);
            }
        }

        public async Task RemoveDatabaseWithUser(string? database, string? username)
        {
            throw new System.NotImplementedException();
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();

        private async Task<bool> DatabaseExists(string name)
        {
            await using var cmd = new MySqlCommand(
                $"SELECT 1 FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{name}';",
                _connection);
            var result = await cmd.ExecuteScalarAsync();

            return result != null;
        }

        private async Task CreateDatabase(string name)
        {
            await using var cmd = new MySqlCommand($"CREATE DATABASE {name};", _connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<bool> UserExists(string name)
        {
            await using var cmd = new MySqlCommand(
                $"SELECT 1 FROM mysql.user WHERE user = '{name}';",
                _connection);
            var result = await cmd.ExecuteScalarAsync();

            return result != null;
        }

        private async Task CreateUser(string name, string password)
        {
            await using var cmd = new MySqlCommand(
                $"CREATE USER '{name}'@'%' IDENTIFIED BY '{password}';",
                _connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<bool> UserHasAccess(string name, string database)
        {
            await using var cmd = new MySqlCommand(
                $"SELECT 1 FROM mysql.db WHERE user = '{name}' AND db = '{database}';",
                _connection);
            var result = await cmd.ExecuteScalarAsync();

            return result != null;
        }

        private async Task AttachUserToDatabase(string username, string database)
        {
            await using var cmd = new MySqlCommand(
                $"GRANT ALL ON `{database}`.* TO '{username}'@'%';",
                _connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
