using CloudFabric.Projections.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CloudFabric.EventSourcing.AspNet;

public class ProjectionsRebuildProcessorHostedService : IHostedService
{
    private readonly ProjectionsRebuildProcessor _projectionsRebuildProcessor;
    private readonly IOptions<ProjectionsRebuildProcessorOptions> _options;
    
    public ProjectionsRebuildProcessorHostedService(
        ProjectionsRebuildProcessor projectionsRebuildProcessor,
        IOptions<ProjectionsRebuildProcessorOptions> options
    ) {
        _projectionsRebuildProcessor = projectionsRebuildProcessor;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _projectionsRebuildProcessor.RebuildProjectionsThatRequireRebuild(_options.Value.MaxParallelTasks, cancellationToken);

            await Task.Delay(1000, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
