using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections.Worker;

public class ProjectionsRebuildProcessor
{
    private readonly ProjectionRepository _projectionRepository;
    private readonly Func<string, ProjectionsEngine> _projectionsEngineFactory;

    private readonly ILogger<ProjectionsRebuildProcessor> _logger;
    
    /// <summary>
    /// </summary>
    /// <param name="projectionRepository"></param>
    /// <param name="projectionsEngineFactory"></param>
    /// <param name="logger"></param>
    public ProjectionsRebuildProcessor(
        ProjectionRepository projectionRepository,
        Func<string, ProjectionsEngine> projectionsEngineFactory,
        ILogger<ProjectionsRebuildProcessor> logger
    ) {
        _projectionRepository = projectionRepository;
        _projectionsEngineFactory = projectionsEngineFactory;
        _logger = logger;
    }

    public async Task DetectProjectionsToRebuild(CancellationToken cancellationToken = default)
    {
        var indexToRebuild = await _projectionRepository.AcquireAndLockProjectionThatRequiresRebuild();

        if (indexToRebuild == null)
        {
            _logger.LogInformation("Nothing to do, did not found an index that requires rebuild");
            return;
        }

        var ind = indexToRebuild.IndexesStatuses.Last();
        
        var connectionId = indexToRebuild.ConnectionId;

        var projectionsEngine = _projectionsEngineFactory(connectionId);

        var eventStoreStatistics = await projectionsEngine.GetEventStoreStatistics();

        ind.TotalEventsToProcess = eventStoreStatistics.TotalEventsCount;
        
        await _projectionRepository.UpdateProjectionRebuildStats(indexToRebuild);
        
        await projectionsEngine.ReplayEventsAsync(
            $"{Environment.MachineName}-{Environment.ProcessId}", null, ind.LastProcessedEventTimestamp,
            250,
            async Task(IEvent lastProcessedEvent) =>
            {
                ind.RebuildEventsProcessed += 250;
                ind.LastProcessedEventTimestamp = lastProcessedEvent.Timestamp;
                ind.RebuildHealthCheckAt = DateTime.UtcNow;
                
                await _projectionRepository.UpdateProjectionRebuildStats(indexToRebuild);
                
                _logger.LogInformation("Processed {EventsProcessed}/{TotalEventsInEventStore}", 
                    ind.RebuildEventsProcessed, ind.TotalEventsToProcess
                );
            },
            cancellationToken
        );

        if (!cancellationToken.IsCancellationRequested)
        {
            ind.RebuildHealthCheckAt = DateTime.UtcNow;
            ind.RebuildCompletedAt = DateTime.UtcNow;

            await _projectionRepository.UpdateProjectionRebuildStats(indexToRebuild);
        }
    }
}
