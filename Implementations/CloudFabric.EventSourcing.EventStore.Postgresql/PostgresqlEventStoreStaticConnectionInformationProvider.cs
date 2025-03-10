using Npgsql;

namespace CloudFabric.EventSourcing.EventStore.Postgresql;


public class PostgresqlEventStoreStaticConnectionInformationProvider : IPostgresqlEventStoreConnectionInformationProvider
{
    private readonly string _eventsTableName;
    private readonly string _itemsTableName;
    private readonly NpgsqlConnectionStringBuilder _connectionStringBuilder;
    private readonly NpgsqlConnectionStringBuilder _connectionStringBuilderWithoutPasswords;

    public PostgresqlEventStoreStaticConnectionInformationProvider(
        string connectionString, 
        string eventsTableName,
        string itemsTableName
    ) {
        _eventsTableName = eventsTableName;
        _itemsTableName = itemsTableName;

        _connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        _connectionStringBuilderWithoutPasswords = new NpgsqlConnectionStringBuilder(connectionString);
        _connectionStringBuilderWithoutPasswords.Password = null;
        _connectionStringBuilderWithoutPasswords.SslPassword = null;
        _connectionStringBuilderWithoutPasswords.SslCertificate = null;
    }

    public PostgresqlEventStoreConnectionInformation GetConnectionInformation(string? connectionId)
    {
        // we don't care about connection id because it's a static provider - connection string will always be the same.
        return new PostgresqlEventStoreConnectionInformation()
        {
            ConnectionId = $"{_connectionStringBuilderWithoutPasswords}-{_eventsTableName}",
            ConnectionString = _connectionStringBuilder.ToString(),
            TableName = _eventsTableName,
            MetadataTableName = _itemsTableName
        };
    }

    EventStoreConnectionInformation IEventStoreConnectionInformationProvider.GetConnectionInformation(string? connectionId)
    {
        return GetConnectionInformation(connectionId);
    }
}