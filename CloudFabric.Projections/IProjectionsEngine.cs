namespace CloudFabric.Projections;

public interface IProjectionsEngine
{
    Task StartAsync(string instanceName);

    Task StopAsync();
}