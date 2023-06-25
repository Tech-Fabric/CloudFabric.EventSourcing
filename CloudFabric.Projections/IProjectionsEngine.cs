namespace CloudFabric.Projections;

public interface IProjectionsEngine
{
    Task StartAsync(string instanceName);

    Task StopAsync();

    // Task StartRebuildAsync(string instanceName, string partitionKey, DateTime? dateFrom = null);
    //
    // Task RebuildOneAsync(Guid documentId, string partitionKey);
    //
    // Task<ProjectionRebuildState?> GetRebuildState(string instanceName, string partitionKey);
}