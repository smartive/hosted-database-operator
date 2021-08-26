namespace HostedDatabaseOperator.Database
{
    public record ConnectionConfiguration(DatabaseType Type, string Host, short Port, string Username, string Password);
}
