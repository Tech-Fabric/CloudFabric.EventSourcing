using CloudFabric.EventSourcing.EventStore;
using Microsoft.Extensions.Logging;

namespace CloudFabric.Projections.Worker;

public class ProjectionsRebuildProcessor
{
    private readonly ProjectionRepository _projectionRepository;
    private readonly Func<string, Task<ProjectionsEngine>> _projectionsEngineFactory;

    private readonly ILogger<ProjectionsRebuildProcessor> _logger;
    
    /// <summary>
    /// </summary>
    /// <param name="projectionRepository"></param>
    /// <param name="projectionsEngineFactory"></param>
    /// <param name="logger"></param>
    public ProjectionsRebuildProcessor(
        ProjectionRepository projectionRepository,
        Func<string, Task<ProjectionsEngine>> projectionsEngineFactory,
        ILogger<ProjectionsRebuildProcessor> logger
    ) {
        _projectionRepository = projectionRepository;
        _projectionsEngineFactory = projectionsEngineFactory;
        _logger = logger;
    }

    public async Task RebuildProjectionsThatRequireRebuild(int maxParallelTasks = 4, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<bool>>();

        for (var i = 0; i < maxParallelTasks; i++)
        {
            try
            {
                var (projectionIndexState, indexNameToRebuild) = await _projectionRepository.AcquireAndLockProjectionThatRequiresRebuild();

                if (projectionIndexState == null || indexNameToRebuild == null)
                {
                    break;
                }

                tasks.Add(RebuildOneProjectionWhichRequiresRebuild(projectionIndexState, indexNameToRebuild, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire and lock projections that require rebuild");
            }
        }

        if (tasks.Count <= 0)
        {
            return;
        }

        await Task.WhenAll(tasks);

        await RebuildProjectionsThatRequireRebuild(maxParallelTasks, cancellationToken);
    }

    public async Task<bool> RebuildOneProjectionWhichRequiresRebuild(
        ProjectionIndexState projectionIndexState, 
        string indexNameToRebuild, 
        CancellationToken cancellationToken = default
    ) {
        try
        {
            var connectionId = projectionIndexState.ConnectionId;

            var projectionsEngine = await _projectionsEngineFactory(connectionId);

            var eventStoreStatistics = await projectionsEngine.GetEventStoreStatistics();

            var indexToRebuild = projectionIndexState.IndexesStatuses.First(i => i.IndexName == indexNameToRebuild);

            indexToRebuild.TotalEventsToProcess = eventStoreStatistics.TotalEventsCount;

            await _projectionRepository.SaveProjectionIndexState(projectionIndexState);

            await projectionsEngine.ReplayEventsAsync(
                $"{Environment.MachineName}-{Environment.ProcessId}", null, indexToRebuild.LastProcessedEventTimestamp,
                250,
                async Task(int eventsProcessed, IEvent lastProcessedEvent) =>
                {
                    indexToRebuild.RebuildEventsProcessed += eventsProcessed;
                    indexToRebuild.LastProcessedEventTimestamp = lastProcessedEvent.Timestamp;
                    indexToRebuild.RebuildHealthCheckAt = DateTime.UtcNow;

                    await _projectionRepository.SaveProjectionIndexState(projectionIndexState);

                    _logger.LogInformation(
                        "Processed {EventsProcessed}/{TotalEventsInEventStore}",
                        indexToRebuild.RebuildEventsProcessed, indexToRebuild.TotalEventsToProcess
                    );
                },
                cancellationToken
            );

            if (!cancellationToken.IsCancellationRequested)
            {
                indexToRebuild.RebuildHealthCheckAt = DateTime.UtcNow;
                indexToRebuild.RebuildCompletedAt = DateTime.UtcNow;

                await _projectionRepository.SaveProjectionIndexState(projectionIndexState);
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding projection {IndexNameToRebuild}", indexNameToRebuild);
        }


        return true;
    }
}