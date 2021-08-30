using System.Data;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HostedDatabaseOperator.Entities;
using KubeOps.Operator.Events;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HostedDatabaseOperator.Database.Flavors
{
    public class PostgresDatabaseHost : IDatabaseHost
    {
        private const byte DatabaseNameMaxLength = 63;
        private const byte UsernameMaxLength = 63;

        private static readonly Regex IllegalChars = new("[^a-zA-Z_-]");
        private static readonly Regex Dashes = new("[-]");

        private readonly ILogger<PostgresDatabaseHost> _logger;
        private readonly NpgsqlConnection _connection;
        private readonly IEventManager.AsyncPublisher _userCreated;
        private readonly IEventManager.AsyncPublisher _databaseCreated;

        public PostgresDatabaseHost(
            ConnectionConfiguration config,
            ILogger<PostgresDatabaseHost> logger,
            IEventManager eventManager)
        {
            ConnectionConfiguration = config;
            _logger = logger;
            _connection = new(ConnectionString());
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

            return true;
        }

        public string ConnectionString(string? databaseName = null) =>
            $"Host={ConnectionConfiguration.Host};Port={ConnectionConfiguration.Port};" +
            $"Username={ConnectionConfiguration.Username};Password={ConnectionConfiguration.Password};" +
            (ConnectionConfiguration.SslMode == SslMode.Disabled
                ? "SslMode=Disable;"
                : "SslMode=Require;") +
            $"{(string.IsNullOrWhiteSpace(databaseName) ? string.Empty : $"Database={databaseName};")}";

        public string FormatDatabaseName(string name)
        {
            var str = Dashes.Replace(IllegalChars.Replace(name, string.Empty), "_").ToLowerInvariant();
            return str.Length > DatabaseNameMaxLength
                ? str[..DatabaseNameMaxLength]
                : str;
        }

        public string FormatUsername(string name)
        {
            var str = Dashes.Replace(IllegalChars.Replace(name, string.Empty), "_").ToLowerInvariant();
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

            if (!await UserExists(username))
            {
                _logger.LogInformation(
                    @"User does not exist, create user ""{user}"" on ""{database}"".",
                    username,
                    dbName);
                await CreateUser(username, password);
                await _userCreated(entity);
            }

            if (!await DatabaseExists(dbName))
            {
                _logger.LogInformation(
                    @"Hosted Database ""{name}"" did not exist. Create Database on host ""{host}"".",
                    dbName,
                    ConnectionConfiguration.Host);
                await CreateDatabase(dbName, username);
                await _databaseCreated(entity);
            }
        }

        public async Task RemoveDatabaseWithUser(string? database, string? username)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            if (database != null && await DatabaseExists(database))
            {
                _logger.LogInformation(
                    @"Hosted Database ""{name}"" exists. Delete Database on host ""{host}"".",
                    database,
                    ConnectionConfiguration.Host);
                await DeleteDatabase(database);
            }

            if (username != null && await UserExists(username))
            {
                _logger.LogInformation(
                    @"User ""{name}"" exists. Delete user on host ""{host}"".",
                    username,
                    ConnectionConfiguration.Host);
                await DeleteUser(username);
            }
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();

        public async Task<bool> DatabaseExists(string name)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var cmd = new NpgsqlCommand(
                $"select 1 from pg_catalog.pg_database where datname ='{name}';",
                _connection);
            var result = await cmd.ExecuteScalarAsync();

            return result != null;
        }

        private async Task CreateDatabase(string dbName, string username)
        {
            await using var roleCmd = new NpgsqlCommand(
                $"grant {username} to {ConnectionConfiguration.Username};",
                _connection);
            await roleCmd.ExecuteNonQueryAsync();

            await using var dbCmd = new NpgsqlCommand($"create database {dbName} owner {username};", _connection);
            await dbCmd.ExecuteNonQueryAsync();
        }

        private async Task<bool> UserExists(string name)
        {
            await using var cmd = new NpgsqlCommand(
                $"SELECT 1 FROM pg_catalog.pg_user WHERE usename = '{name}';",
                _connection);
            var result = await cmd.ExecuteScalarAsync();

            return result != null;
        }

        private async Task CreateUser(string name, string password)
        {
            await using var cmd = new NpgsqlCommand(
                $"CREATE USER {name} WITH ENCRYPTED PASSWORD '{password}';",
                _connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task DeleteDatabase(string database)
        {
            await using var deleteDb = new NpgsqlCommand(
                $"drop database {database};",
                _connection);
            await deleteDb.ExecuteNonQueryAsync();
        }

        private async Task DeleteUser(string username)
        {
            await using var deleteDb = new NpgsqlCommand(
                $"drop role {username};",
                _connection);
            await deleteDb.ExecuteNonQueryAsync();
        }
    }
}
