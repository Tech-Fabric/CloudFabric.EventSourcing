namespace CloudFabric.EventSourcing.EventStore.Postgresql;

public class PostgresqlEventStoreFactory : IEventStoreFactory
{
    public IEventStore CreateEventStore(
        IEventStoreConnectionInformationProvider connectionInformationProvider,
        string? connectionId = null
    ) {
        PostgresqlEventStoreConnectionInformation connectionInformation = 
            (PostgresqlEventStoreConnectionInformation)connectionInformationProvider
                .GetConnectionInformation(connectionId);

        return new PostgresqlEventStore(
            connectionInformation.ConnectionString, 
            connectionInformation.TableName,
            connectionInformation.ItemsTableName
        );
    }
}
