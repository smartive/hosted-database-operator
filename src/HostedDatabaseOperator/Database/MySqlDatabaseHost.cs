using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace HostedDatabaseOperator.Database
{
    public class MySqlDatabaseHost : IDatabaseHost
    {
        private const int DatabaseNameMaxLength = 64;
        private const int UsernameMaxLength = 16;

        private static readonly Regex IllegalChars = new Regex("[^a-zA-Z_-]");
        private static readonly Regex Dashes = new Regex("[-]");

        private readonly MySqlConnection _connection;

        public MySqlDatabaseHost(ConnectionConfig config)
        {
            Config = config;
            var connStr = $"server={config.Host};port={config.Port};user={config.Username};password={config.Password};";
            _connection = new MySqlConnection(connStr);
        }

        public ConnectionConfig Config { get; }

        public async Task<bool> CanConnect()
        {
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

        public async Task<bool> DatabaseExists(string name)
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

        public async Task CreateDatabase(string name)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var cmd = new MySqlCommand($"CREATE DATABASE {name};", _connection);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ClearDatabaseUsers(string name)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var cmd = new MySqlCommand($"SELECT `User` FROM mysql.db where `Db` = '{name}';", _connection);
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

        public async Task DeleteDatabase(string name)
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

        public async Task<bool> UserExists(string name)
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

        public async Task<string> CreateUser(string name)
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            var password = GetRandomPassword();
            await using var cmd = new MySqlCommand(
                $"CREATE USER '{name}'@'%' IDENTIFIED BY '{password}';",
                _connection);
            await cmd.ExecuteNonQueryAsync();

            return password;
        }

        public async Task<bool> UserHasAccess(string name, string database)
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

        public async Task AttachUserToDatabase(string username, string database)
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

        public ValueTask DisposeAsync() => _connection.DisposeAsync();

        private static string GetRandomPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                                 "abcdefghijklmnopqrstuvwxyz" +
                                 "0123456789";
            var rnd = new Random(DateTime.Now.Millisecond);

            var stringBuilder = new StringBuilder();
            for (var x = 0; x < 16; x++)
            {
                var index = rnd.Next(0, chars.Length - 1);
                stringBuilder.Append(chars[index]);
            }

            return stringBuilder.ToString();
        }
    }
}
