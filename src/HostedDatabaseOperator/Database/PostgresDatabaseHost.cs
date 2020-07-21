using System;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HostedDatabaseOperator.Database
{
    public class PostgresDatabaseHost : IDatabaseHost
    {
        private readonly ILogger _logger;
        private const int DatabaseNameMaxLength = 63;
        private const int UsernameMaxLength = 63;

        private static readonly Regex IllegalChars = new Regex("[^a-zA-Z_-]");
        private static readonly Regex Dashes = new Regex("[-]");

        private readonly NpgsqlConnection _connection;

        public PostgresDatabaseHost(ILogger logger, ConnectionConfig config)
        {
            _logger = logger;
            Config = config;
            var connStr =
                $"Host={config.Host};Port={config.Port};Username={config.Username};Password={config.Password};";
            _connection = new NpgsqlConnection(connStr);
        }

        public ConnectionConfig Config { get; }

        public async Task<bool> CanConnect()
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            return true;
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
            var str = Dashes.Replace(IllegalChars.Replace(name, string.Empty), "_");
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
             *     1. Check if the user exists
             *         1.a if not: create it
             *     2. Check if the database exists.
             *         2.a if not: create it and set owner to user
             */

            if (!await UserExists(userName))
            {
                _logger.LogInformation(
                    @"User did not exist, create user ""{user}"".",
                    userName);
                result = await CreateUser(userName);
            }

            if (!await DatabaseExists(dbName))
            {
                _logger.LogInformation(
                    @"Hosted Database ""{name}"" did not exist. Create Database.",
                    dbName);
                await CreateDatabase(dbName, userName);
            }

            return result;
        }

        public async Task Teardown(string dbName)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var owner = new NpgsqlCommand(
                $@"SELECT d.datname as ""Name"",
                   pg_catalog.pg_get_userbyid(d.datdba) as ""Owner""
                   FROM pg_catalog.pg_database d
                   WHERE d.datname = '{dbName}';",
                _connection);
            await using var ownerReader = await owner.ExecuteReaderAsync();
            await ownerReader.ReadAsync(); // Only one user can be owner.
            var user = ownerReader["Owner"]?.ToString() ?? string.Empty;
            await ownerReader.CloseAsync();

            await using var dropOwned = new NpgsqlCommand(
                $"drop owned by {user};",
                _connection);
            await dropOwned.ExecuteNonQueryAsync();
            await using var dropDb = new NpgsqlCommand(
                $"drop database {dbName};",
                _connection);
            await dropDb.ExecuteNonQueryAsync();
            await using var dropUser = new NpgsqlCommand(
                $"drop user {user};",
                _connection);
            await dropUser.ExecuteNonQueryAsync();
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();

        private async Task<bool> UserExists(string name)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var cmd = new NpgsqlCommand(
                $"SELECT 1 FROM pg_catalog.pg_user WHERE usename = '{name}';",
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
            await using var cmd = new NpgsqlCommand(
                $"CREATE USER {name} WITH ENCRYPTED PASSWORD '{password}';",
                _connection);
            await cmd.ExecuteNonQueryAsync();

            return password;
        }

        private async Task<bool> DatabaseExists(string name)
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

        private async Task CreateDatabase(string dbName, string userName)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var dbCmd = new NpgsqlCommand($"create database {dbName} owner {userName};", _connection);
            await dbCmd.ExecuteNonQueryAsync();

            await using var userCmd =
                new NpgsqlCommand($"grant all privileges on database {dbName} to {userName};", _connection);
            await userCmd.ExecuteNonQueryAsync();
        }

        //
        // public async Task ClearDatabaseUsers(string name)
        // {
        //     if (_connection.State != ConnectionState.Open)
        //     {
        //         await _connection.OpenAsync();
        //     }
        //
        //     await using var cmd = new NpgsqlCommand(
        //         $@"
        //         SELECT usename FROM pg_catalog.pg_user where
        //         pg_catalog.has_schema_privilege(usename, '{name}', 'USAGE')
        //         and usename != 'postgres';",
        //         _connection);
        //     await using var userReader = await cmd.ExecuteReaderAsync();
        //
        //     var users = new List<string>();
        //     while (await userReader.ReadAsync())
        //     {
        //         users.Add(userReader["usename"]?.ToString() ?? string.Empty);
        //     }
        //
        //     await userReader.CloseAsync();
        //
        //     if (users.Count <= 0)
        //     {
        //         return;
        //     }
        //
        //     foreach (var usr in users)
        //     {
        // await using var owned = new NpgsqlCommand(
        //     $"drop owned by {usr};",
        //     _connection);
        // await owned.ExecuteNonQueryAsync();
        // await using var user = new NpgsqlCommand(
        //     $"drop user {usr};",
        //     _connection);
        // await user.ExecuteNonQueryAsync();
        //     }
        // }
        //
        // public async Task DeleteDatabase(string name)
        // {
        //     if (_connection.State != ConnectionState.Open)
        //     {
        //         await _connection.OpenAsync();
        //     }
        //
        //     await using var deleteDb = new NpgsqlCommand(
        //         $"DROP SCHEMA IF EXISTS {name} CASCADE;",
        //         _connection);
        //     await deleteDb.ExecuteNonQueryAsync();
        // }
        //

        //

        //
        // public async Task<bool> UserHasAccess(string name, string database)
        // {
        //     if (_connection.State != ConnectionState.Open)
        //     {
        //         await _connection.OpenAsync();
        //     }
        //
        //     await using var cmd = new NpgsqlCommand(
        //         $@"SELECT distinct table_catalog, grantee
        //            FROM information_schema.table_privileges
        //            WHERE grantee = '{name}' AND table_catalog = '{database}';",
        //         _connection);
        //     var result = await cmd.ExecuteScalarAsync();
        //
        //     return result != null;
        // }
        //
        // public async Task AttachUserToDatabase(string username, string database)
        // {
        //     if (_connection.State != ConnectionState.Open)
        //     {
        //         await _connection.OpenAsync();
        //     }
        //
        //     await using var db = new NpgsqlCommand(
        //         $"GRANT CONNECT ON DATABASE postgres TO {username};",
        //         _connection);
        //     await db.ExecuteNonQueryAsync();
        //     await using var schema = new NpgsqlCommand(
        //         $"GRANT ALL ON SCHEMA {database} TO {username};",
        //         _connection);
        //     await schema.ExecuteNonQueryAsync();
        // }
        //
        // public ValueTask DisposeAsync() => _connection.DisposeAsync();
        //
        // private static string GetRandomPassword()
        // {
        //     const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
        //                          "abcdefghijklmnopqrstuvwxyz" +
        //                          "0123456789";
        //     var rnd = new Random(DateTime.Now.Millisecond);
        //
        //     var stringBuilder = new StringBuilder();
        //     for (var x = 0; x < 16; x++)
        //     {
        //         var index = rnd.Next(0, chars.Length - 1);
        //         stringBuilder.Append(chars[index]);
        //     }
        //
        //     return stringBuilder.ToString();
        // }
    }
}
