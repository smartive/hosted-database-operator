using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Database
{
    public class DatabaseConnectionsPool
    {
        private readonly ILoggerFactory _factory;

        private readonly IDictionary<string, ConnectionConfiguration> _configs =
            new ConcurrentDictionary<string, ConnectionConfiguration>();

        public DatabaseConnectionsPool(ILoggerFactory factory)
        {
            _factory = factory;
        }

        public void Add(string name, ConnectionConfiguration config) => _configs[name] = config;

        public ConnectionConfiguration Get(string name) => _configs[name];

        public void Remove(string name) => _configs.Remove(name);

        public IDatabaseHost GetHost(string name) => GetHost(Get(name));

        public IDatabaseHost GetHost(ConnectionConfiguration config) => config.Type switch
        {
            DatabaseType.MySql => new MySqlDatabaseHost(config, _factory.CreateLogger<MySqlDatabaseHost>()),
            // DatabaseType.Postgres => new PostgresDatabaseHost(_factory.CreateLogger(typeof(PostgresDatabaseHost)), config),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
