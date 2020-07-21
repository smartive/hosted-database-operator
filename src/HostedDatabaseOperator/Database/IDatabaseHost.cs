using System;
using System.Threading.Tasks;

namespace HostedDatabaseOperator.Database
{
    public interface IDatabaseHost : IAsyncDisposable
    {
        ConnectionConfig Config { get; }

        Task<bool> CanConnect();

        string FormatDatabaseName(string name);

        string FormatUsername(string name);

        Task<string?> ProcessDatabase(string dbName, string userName);

        Task Teardown(string dbName);

        // Task<bool> DatabaseExists(string name);
        //
        // Task CreateDatabase(string name);
        //
        // Task ClearDatabaseUsers(string name);
        //
        // Task DeleteDatabase(string name);
        //
        // Task<bool> UserExists(string name);
        //
        // Task<string> CreateUser(string name);
        //
        // Task<bool> UserHasAccess(string name, string database);
        //
        // Task AttachUserToDatabase(string username, string database);
    }
}
