using Npgsql;

namespace CloudFabric.EventSourcing.EventStore.Postgresql;

public interface IPostgresqlEventStoreConnectionInformationProvider : IEventStoreConnectionInformationProvider
{
    public new PostgresqlEventStoreConnectionInformation GetConnectionInformation(string? connectionId = null);
}
