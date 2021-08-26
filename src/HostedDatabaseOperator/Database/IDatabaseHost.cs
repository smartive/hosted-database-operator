using System;
using System.Text;
using System.Threading.Tasks;
using HostedDatabaseOperator.Entities;

namespace HostedDatabaseOperator.Database
{
    public interface IDatabaseHost : IAsyncDisposable
    {
        public const byte PasswordLength = 16;

        string GeneratePassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                                 "abcdefghijklmnopqrstuvwxyz" +
                                 "0123456789";
            var rnd = new Random(DateTime.UtcNow.Millisecond);

            var stringBuilder = new StringBuilder();
            for (byte x = 0; x < PasswordLength; x++)
            {
                var index = rnd.Next(0, chars.Length - 1);
                stringBuilder.Append(chars[index]);
            }

            return stringBuilder.ToString();
        }

        ConnectionConfiguration ConnectionConfiguration { get; }

        Task<bool> CanConnect();

        string ConnectionString(string? databaseName = null);

        string FormatDatabaseName(string name);

        string FormatUsername(string name);

        Task UpsertDatabase(HostedDatabase entity, string dbName, string username, string password);

        Task RemoveDatabaseWithUser(string? database, string? username);

        Task<bool> DatabaseExists(string database);

        // Task CreateDanglingDatabase(string dbName);
        //
    }
}
