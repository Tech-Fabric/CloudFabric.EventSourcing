namespace CloudFabric.Projections;

public interface IProjectionsEngine
{
    Task StartAsync(string instanceName);

    Task StopAsync();

    Task RebuildAsync(DateTime? dateFrom = null);

    Task RebuildOneAsync(string documentId);
}