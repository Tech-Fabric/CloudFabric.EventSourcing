namespace CloudFabric.Projections;

public interface IProjectionsEngine
{
    Task StartAsync(string instanceName);

    Task StopAsync();

    Task RebuildAsync(string instanceName, string partitionKey, DateTime? dateFrom = null);

    Task RebuildOneAsync(string documentId, string partitionKey);

    Task<ProjectionRebuildState> GetRebuildState(string instanceName, string partitionKey);
}