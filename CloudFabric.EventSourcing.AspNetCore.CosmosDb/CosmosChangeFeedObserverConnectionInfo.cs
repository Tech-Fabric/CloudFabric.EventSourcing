using Microsoft.Azure.Cosmos;

namespace CloudFabric.EventSourcing.AspNetCore.CosmosDb;

public class CosmosChangeFeedObserverConnectionInfo
{
    public CosmosClient EventsClient { get; set; }

    public string EventsDatabaseId { get; set; }

    public string EventsContainerId { get; set; }

    public CosmosClient LeaseClient { get; set; }

    public string LeaseDatabaseId { get; set; }

    public string LeaseContainerId { get; set; }

    public string ProcessorName { get; set; }
}
