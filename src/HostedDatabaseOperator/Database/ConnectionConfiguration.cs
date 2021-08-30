namespace HostedDatabaseOperator.Database
{
    public record ConnectionConfiguration(
        DatabaseType Type,
        string Host,
        short Port,
        SslMode SslMode,
        string Username,
        string Password);
}
