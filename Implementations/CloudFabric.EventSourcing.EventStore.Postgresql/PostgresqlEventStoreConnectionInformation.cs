namespace CloudFabric.EventSourcing.EventStore.Postgresql;

public class PostgresqlEventStoreConnectionInformation: EventStoreConnectionInformation
{
    public string ConnectionString { get; set; }
    
    /// <summary>
    /// Table where all events will be stored.
    /// </summary>
    public string TableName { get; set; }

    public string ItemsTableName { get; set; }
}
