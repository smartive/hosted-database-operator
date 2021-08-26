using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HostedDatabaseOperator.Database.Flavors;
using KubeOps.Operator.Events;
using Microsoft.Extensions.Logging;

namespace HostedDatabaseOperator.Database
{
    public class DatabaseConnectionPool
    {
        private readonly ILoggerFactory _factory;
        private readonly IEventManager _eventManager;

        private readonly IDictionary<string, ConnectionConfiguration> _configs =
            new ConcurrentDictionary<string, ConnectionConfiguration>();

        public DatabaseConnectionPool(ILoggerFactory factory, IEventManager eventManager)
        {
            _factory = factory;
            _eventManager = eventManager;
        }

        public void Add(string name, ConnectionConfiguration config) => _configs[name] = config;

        public ConnectionConfiguration Get(string name) => _configs[name];

        public void Remove(string name) => _configs.Remove(name);

        public IDatabaseHost GetHost(string name) => GetHost(Get(name));

        public IDatabaseHost GetHost(ConnectionConfiguration config) => config.Type switch
        {
            DatabaseType.MySql => new MySqlDatabaseHost(
                config,
                _factory.CreateLogger<MySqlDatabaseHost>(),
                _eventManager),
            DatabaseType.Postgres => new PostgresDatabaseHost(
                config,
                _factory.CreateLogger<PostgresDatabaseHost>(),
                _eventManager),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
