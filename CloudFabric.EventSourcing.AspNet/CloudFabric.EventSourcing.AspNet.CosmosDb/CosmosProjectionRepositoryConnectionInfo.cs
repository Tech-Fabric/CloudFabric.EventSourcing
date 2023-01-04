using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace CloudFabric.EventSourcing.AspNet.CosmosDb;

public class CosmosProjectionRepositoryConnectionInfo
{
    public ILoggerFactory LoggerFactory { get; set; }

    public string ConnectionString { get; set; }

    public CosmosClientOptions CosmosClientOptions { get; set; }

    public string DatabaseId { get; set; }

    public string ContainerId { get; set; }

    public string PartitionKey { get; set; }
}
