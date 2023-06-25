using Npgsql;

namespace CloudFabric.EventSourcing.EventStore.Postgresql;


public class PostgresqlEventStoreStaticConnectionInformationProvider : IPostgresqlEventStoreConnectionInformationProvider
{
    private readonly string _tableName;
    private readonly NpgsqlConnectionStringBuilder _connectionStringBuilder;
    private readonly NpgsqlConnectionStringBuilder _connectionStringBuilderWithoutPasswords;

    public PostgresqlEventStoreStaticConnectionInformationProvider(
        string connectionString, 
        string tableName
    ) {
        _tableName = tableName;

        _connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        _connectionStringBuilderWithoutPasswords = new NpgsqlConnectionStringBuilder(connectionString);
        _connectionStringBuilderWithoutPasswords.Password = "";
        _connectionStringBuilderWithoutPasswords.SslPassword = "";
        _connectionStringBuilderWithoutPasswords.SslCertificate = "";
    }

    public PostgresqlEventStoreConnectionInformation GetConnectionInformation(string? connectionId)
    {
        // we don't care about connection id because it's a static provider - connection string will always be the same.
        return new PostgresqlEventStoreConnectionInformation()
        {
            ConnectionId = $"{_connectionStringBuilderWithoutPasswords}-{_tableName}",
            ConnectionString = _connectionStringBuilder.ToString(),
            TableName = _tableName
        };
    }

    EventStoreConnectionInformation IEventStoreConnectionInformationProvider.GetConnectionInformation(string? connectionId)
    {
        return GetConnectionInformation(connectionId);
    }
}