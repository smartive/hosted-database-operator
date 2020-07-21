using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Database
{
    public class ConnectionsManager
    {
        private readonly ILoggerFactory _factory;

        private readonly IDictionary<string, ConnectionConfig> Configs =
            new ConcurrentDictionary<string, ConnectionConfig>();

        public ConnectionsManager(ILoggerFactory factory)
        {
            _factory = factory;
        }

        public void Add(string name, ConnectionConfig config) => Configs[name] = config;

        public ConnectionConfig Get(string name) => Configs[name];

        public void Remove(string name) => Configs.Remove(name);

        public IDatabaseHost GetHost(string name) => GetHost(Get(name));

        public IDatabaseHost GetHost(ConnectionConfig config) => config.Type switch
        {
            DatabaseType.MySql => new MySqlDatabaseHost(_factory.CreateLogger(typeof(MySqlDatabaseHost)), config),
            DatabaseType.Postgres => new PostgresDatabaseHost(_factory.CreateLogger(typeof(PostgresDatabaseHost)), config),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
