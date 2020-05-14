using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HostedDatabaseOperator.Database
{
    public class ConnectionsManager
    {
        private readonly IDictionary<string, ConnectionConfig> Configs =
            new ConcurrentDictionary<string, ConnectionConfig>();

        public void Add(string name, ConnectionConfig config) => Configs[name] = config;

        public ConnectionConfig Get(string name) => Configs[name];

        public void Remove(string name) => Configs.Remove(name);

        public IDatabaseHost GetHost(string name) => GetHost(Get(name));

        public IDatabaseHost GetHost(ConnectionConfig config) => config.Type switch
        {
            DatabaseType.MySql => new MySqlDatabaseHost(config),
            DatabaseType.Postgres => new PostgresDatabaseHost(config),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
