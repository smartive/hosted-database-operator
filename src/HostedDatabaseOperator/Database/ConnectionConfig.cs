namespace HostedDatabaseOperator.Database
{
    public class ConnectionConfig
    {
        public DatabaseType Type { get; set; }

        public string Host { get; set; } = string.Empty;

        public int Port { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
