using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace HostedDatabaseOperator.Database
{
    public class MySqlDatabaseHost : IDatabaseHost
    {
        private readonly ILogger _logger;
        private const int DatabaseNameMaxLength = 64;
        private const int UsernameMaxLength = 16;

        private static readonly Regex IllegalChars = new Regex("[^a-zA-Z_-]");
        private static readonly Regex Dashes = new Regex("[-]");

        private readonly MySqlConnection _connection;

        public MySqlDatabaseHost(ILogger logger, ConnectionConfig config)
        {
            _logger = logger;
            Config = config;
            var connStr = $"server={config.Host};port={config.Port};user={config.Username};password={config.Password};";
            _connection = new MySqlConnection(connStr);
        }

        public ConnectionConfig Config { get; }

        public async Task<bool> CanConnect()
        {
            _logger.LogTrace("Check if can connect to db.");
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            return _connection.Ping();
        }

        public string FormatDatabaseName(string name)
        {
            var str = Dashes.Replace(IllegalChars.Replace(name, string.Empty), "_");
            return str.Length > DatabaseNameMaxLength
                ? str.Substring(0, DatabaseNameMaxLength)
                : str;
        }

        public string FormatUsername(string name)
        {
            var str = IllegalChars.Replace(name, string.Empty);
            return str.Length > UsernameMaxLength
                ? str.Substring(0, UsernameMaxLength)
                : str;
        }

        public async Task<string?> ProcessDatabase(string dbName, string userName)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            string? result = null;

            /*
             * Steps:
             *     1. Check if the database exists.
             *         1.a if not: create it
             *     2. Check if the user exists
             *         2.a if not: create it
             *     3. Check if the user has access
             *         3.a if not: attach it
             */

            if (!await DatabaseExists(dbName))
            {
                _logger.LogInformation(
                    @"Hosted Database ""{name}"" did not exist. Create Database.",
                    dbName);
                await CreateDatabase(dbName);
            }

            if (!await UserExists(userName))
            {
                _logger.LogInformation(
                    @"User did not exist, create user ""{user}"".",
                    userName);
                result = await CreateUser(userName);
            }

            if (!await UserHasAccess(userName, dbName))
            {
                _logger.LogInformation("User has no access. Reset access.");
                await ClearDatabaseUsers(dbName, userName);
                await AttachUserToDatabase(userName, dbName);
            }

            return result;
        }

        public async Task Teardown(string dbName)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await ClearDatabaseUsers(dbName);
            await DeleteDatabase(dbName);
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();

        private async Task<bool> DatabaseExists(string name)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var cmd = new MySqlCommand(
                $"SELECT 1 FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{name}';",
                _connection);
            var result = await cmd.ExecuteScalarAsync();

            return result != null;
        }

        private async Task CreateDatabase(string name)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var cmd = new MySqlCommand($"CREATE DATABASE {name};", _connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<bool> UserExists(string name)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var cmd = new MySqlCommand(
                $"SELECT 1 FROM mysql.user WHERE user = '{name}';",
                _connection);
            var result = await cmd.ExecuteScalarAsync();

            return result != null;
        }

        private async Task<string> CreateUser(string name)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            var password = this.GenerateRandomPassword();
            await using var cmd = new MySqlCommand(
                $"CREATE USER '{name}'@'%' IDENTIFIED BY '{password}';",
                _connection);
            await cmd.ExecuteNonQueryAsync();

            return password;
        }

        private async Task<bool> UserHasAccess(string name, string database)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var cmd = new MySqlCommand(
                $"SELECT 1 FROM mysql.db WHERE user = '{name}' AND db = '{database}';",
                _connection);
            var result = await cmd.ExecuteScalarAsync();

            return result != null;
        }

        private async Task ClearDatabaseUsers(string dbName, string? owner = null)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var cmd = new MySqlCommand(
                $"SELECT `User` FROM mysql.db where `Db` = '{dbName}'{(string.IsNullOrEmpty(owner) ? string.Empty : $" and `User` != '{owner}'")};",
                _connection);
            await using var userReader = await cmd.ExecuteReaderAsync();

            var users = new List<string>();
            while (await userReader.ReadAsync())
            {
                users.Add(userReader["User"]?.ToString() ?? string.Empty);
            }

            await userReader.CloseAsync();

            if (users.Count <= 0)
            {
                return;
            }

            await using var deleteUser = new MySqlCommand(
                $"DROP USER IF EXISTS {string.Join(',', users.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => $"'{u}'@'%'"))};",
                _connection);
            await deleteUser.ExecuteNonQueryAsync();
        }

        private async Task AttachUserToDatabase(string username, string database)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var cmd = new MySqlCommand(
                $"GRANT ALL ON `{database}`.* TO '{username}'@'%';",
                _connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task DeleteDatabase(string name)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var deleteDb = new MySqlCommand(
                $"DROP DATABASE IF EXISTS {name};",
                _connection);
            await deleteDb.ExecuteNonQueryAsync();
        }
    }
}
