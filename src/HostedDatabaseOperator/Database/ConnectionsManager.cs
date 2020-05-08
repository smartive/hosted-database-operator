using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HostedDatabaseOperator.Database
{
    public static class ConnectionsManager
    {
        private static readonly IDictionary<string, ConnectionConfig> Configs =
            new ConcurrentDictionary<string, ConnectionConfig>();

        public static void Add(string name, ConnectionConfig config) => Configs[name] = config;

        public static ConnectionConfig Get(string name) => Configs[name];

        public static void Remove(string name) => Configs.Remove(name);

        public static IDatabaseHost GetHost(string name) => GetHost(Get(name));

        public static IDatabaseHost GetHost(ConnectionConfig config) => config.Type switch
        {
            DatabaseType.MySql => new MySqlDatabaseHost(config),
            DatabaseType.Postgres => new PostgresDatabaseHost(config),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
