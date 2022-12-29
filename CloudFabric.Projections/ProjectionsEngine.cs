using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;

public class ProjectionsEngine : IProjectionsEngine
{
    private readonly List<IProjectionBuilder<ProjectionDocument>> _projectionBuilders = new();
    private readonly List<IProjectionBuilder> _dynamicProjectionBuilders = new();
    
    private IEventsObserver? _observer;
    private readonly IProjectionRepository<ProjectionRebuildState> _projectionsStateRepository;

    public ProjectionsEngine(IProjectionRepository<ProjectionRebuildState> projectionsStateRepository)
    {
        _projectionsStateRepository = projectionsStateRepository;
    }

    public Task StartAsync(string instanceName)
    {
        if (_observer == null)
        {
            throw new InvalidOperationException("SetEventsObserver should be called before StartAsync");
        }

        return _observer.StartAsync(instanceName);
    }

    public Task StopAsync()
    {
        if (_observer == null)
        {
            throw new InvalidOperationException("SetEventsObserver should be called before StopAsync");
        }

        return _observer.StopAsync();
    }

    public void SetEventsObserver(IEventsObserver eventsObserver)
    {
        _observer = eventsObserver;
        _observer.SetEventHandler(HandleEvent);
    }

    public void AddProjectionBuilder(IProjectionBuilder<ProjectionDocument> projectionBuilder)
    {
        _projectionBuilders.Add(projectionBuilder);
    }
    
    public void AddProjectionBuilder(IProjectionBuilder projectionBuilder)
    {
        _dynamicProjectionBuilders.Add(projectionBuilder);
    }

    public async Task RebuildAsync(string instanceName, string partitionKey, DateTime? dateFrom = null)
    {
        await _projectionsStateRepository.Upsert(new ProjectionRebuildState
        {
            Id = Guid.NewGuid(),
            PartitionKey = partitionKey,
            InstanceName = instanceName,
            Status = RebuildStatus.Running
        }, 
        partitionKey,
        DateTime.UtcNow);

        // run in background
        _observer.LoadAndHandleEventsAsync(instanceName, partitionKey, dateFrom, OnRebuildCompleted, OnRebuildFailed);
    }

    public async Task RebuildOneAsync(Guid documentId, string partitionKey)
    {
        await _observer.LoadAndHandleEventsForDocumentAsync(documentId, partitionKey);
    }

    public async Task<ProjectionRebuildState> GetRebuildState(string instanceName, string partitionKey)
    {
        var rebuildState = (await _projectionsStateRepository.Query(
            ProjectionQuery.Where<ProjectionRebuildState>(x => x.InstanceName == instanceName),
            partitionKey: partitionKey
        ))
        .Records
        .LastOrDefault();

        return rebuildState.Document;
    }

    private async Task HandleEvent(IEvent @event)
    {
        foreach (var projectionBuilder in
                 _projectionBuilders.Where(p => p.HandledEventTypes.Contains(@event.GetType())))
        {
            await projectionBuilder.ApplyEvent(@event); 
        }
        
        foreach (var projectionBuilder in
                 _dynamicProjectionBuilders.Where(p => p.HandledEventTypes.Contains(@event.GetType())))
        {
            await projectionBuilder.ApplyEvent(@event); 
        }
        
        #region Apply AggregateUpdatedEvent to projection builders
        
        if (!@event.AggregateId.HasValue)
        {
            return;
        }
        
        var aggregateType = Type.GetType(@event.AggregateType);
        var aggregateUpdatedEventType = typeof(AggregateUpdatedEvent<>).MakeGenericType(aggregateType);

        var buildersWithAggregateUpdatedEvent = _projectionBuilders.Where(
            p => !p.HandledEventTypes.Contains(@event.GetType()) && p.HandledEventTypes.Contains(aggregateUpdatedEventType)
        );

        var dynamicBuildersWithAggregateUpdatedEvent = _dynamicProjectionBuilders.Where(
            p => !p.HandledEventTypes.Contains(@event.GetType()) && p.HandledEventTypes.Contains(aggregateUpdatedEventType)
        );

        if (buildersWithAggregateUpdatedEvent.Any() || dynamicBuildersWithAggregateUpdatedEvent.Any())
        {
            var aggregateUpdatedEvent = (IEvent)Activator.CreateInstance(aggregateUpdatedEventType)!;
            aggregateUpdatedEvent.AggregateId = @event.AggregateId;
            aggregateUpdatedEvent.PartitionKey = @event.PartitionKey;
            aggregateUpdatedEvent.AggregateType = @event.AggregateType;
            aggregateUpdatedEventType.GetProperty(nameof(AggregateUpdatedEvent<object>.UpdatedAt))!.SetValue(aggregateUpdatedEvent, @event.Timestamp);

            foreach (var projectionBuilder in buildersWithAggregateUpdatedEvent)
            {
                await projectionBuilder.ApplyEvent(aggregateUpdatedEvent);
            }

            foreach (var projectionBuilder in dynamicBuildersWithAggregateUpdatedEvent)
            {
                await projectionBuilder.ApplyEvent(aggregateUpdatedEvent);
            }
        }

        #endregion
    }

    private async Task OnRebuildCompleted(string instanceName, string partitionKey)
    {
        var rebuildState = (await _projectionsStateRepository.Query(
            ProjectionQuery.Where<ProjectionRebuildState>(x => x.InstanceName == instanceName)
        ))
        .Records
        .Select(x => x.Document)
        .FirstOrDefault();
        
        if (rebuildState == null)
        {
            rebuildState = new ProjectionRebuildState
            {
                Id = Guid.NewGuid(),
                InstanceName = instanceName
            };
        }
        
        rebuildState.Status = RebuildStatus.Completed;

        await _projectionsStateRepository.Upsert(rebuildState, partitionKey, DateTime.UtcNow);
    }

    private async Task OnRebuildFailed(string instanceName, string partitionKey, string errorMessage)
    {
        var rebuildState = (await _projectionsStateRepository.Query(
            ProjectionQuery.Where<ProjectionRebuildState>(x => x.InstanceName == instanceName)
        ))
        .Records
        .Select(x => x.Document)
        .FirstOrDefault();
        
        if (rebuildState == null)
        {
            rebuildState = new ProjectionRebuildState
            {
                Id = Guid.NewGuid(),
                InstanceName = instanceName
            };
        }
        
        rebuildState.Status = RebuildStatus.Failed;
        rebuildState.ErrorMessage = errorMessage;

        await _projectionsStateRepository.Upsert(rebuildState, partitionKey, DateTime.UtcNow);
    }
}
